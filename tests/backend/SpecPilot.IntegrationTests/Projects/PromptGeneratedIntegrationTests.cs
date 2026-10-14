using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpecPilot.Domain.Enums;
using SpecPilot.Infrastructure.Persistence;
using SpecPilot.IntegrationTests.Auth;
using SpecPilot.IntegrationTests.Infrastructure;

namespace SpecPilot.IntegrationTests.Projects;

public class PromptGeneratedIntegrationTests : IClassFixture<SpecPilotApiFactory>
{
    private readonly SpecPilotApiFactory _factory;

    public PromptGeneratedIntegrationTests(SpecPilotApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Contract_should_return_persisted_answers_in_question_order()
    {
        var client = await CreateAuthenticatedClientAsync();
        var projectId = await CreateProjectAsync(client);
        var questions = await GenerateQuestionsAsync(client, projectId);
        var answers = questions.Select((question, index) => new
        {
            questionId = question.Id,
            answer = $"Resposta contratual {index + 1}"
        }).ToArray();

        var answerResponse = await client.PutAsJsonAsync($"/api/projects/{projectId}/questions/answers", new { answers });
        answerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.GetAsync($"/api/projects/{projectId}/questions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<QuestionsContract>();
        body.Should().NotBeNull();
        body!.ProjectId.Should().Be(projectId);
        body.Status.Should().Be("QuestionsAnswered");
        body.Questions.Select(x => x.Id).Should().Equal(questions.Select(x => x.Id));
        body.Questions.Select(x => x.Answer).Should().Equal(answers.Select(x => x.answer));
    }

    [Fact]
    public async Task Contract_should_reject_document_generation_without_token()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync($"/api/projects/{Guid.NewGuid()}/generate-document", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsContract>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(401);
        problem.Extensions["code"].GetString().Should().Be("auth.unauthorized");
    }

    [Fact]
    public async Task Workflow_should_reject_answers_from_another_project_without_partial_update()
    {
        var client = await CreateAuthenticatedClientAsync();
        var targetProjectId = await CreateProjectAsync(client);
        var otherProjectId = await CreateProjectAsync(client);
        var targetQuestions = await GenerateQuestionsAsync(client, targetProjectId);
        var otherQuestions = await GenerateQuestionsAsync(client, otherProjectId);
        var mixedAnswers = targetQuestions.Select((question, index) => new
        {
            questionId = index == 0 ? otherQuestions[0].Id : question.Id,
            answer = $"Resposta {index + 1}"
        }).ToArray();

        var response = await client.PutAsJsonAsync($"/api/projects/{targetProjectId}/questions/answers", new { answers = mixedAnswers });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsContract>();
        problem.Should().NotBeNull();
        problem!.Extensions["code"].GetString().Should().Be("projects.incomplete_answers");

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SpecPilotDbContext>();
            (await context.Projects.AsNoTracking().SingleAsync(x => x.Id == targetProjectId))
                .Status.Should().Be(ProjectStatus.QuestionsGenerated);
            var persistedQuestions = await context.RefinementQuestions.AsNoTracking()
                .Where(x => x.ProjectId == targetProjectId)
                .ToListAsync();
            persistedQuestions.Should().OnlyContain(x => x.AnswerText == null && x.AnsweredAtUtc == null);
        }

        var validAnswers = targetQuestions.Select((question, index) => new
        {
            questionId = question.Id,
            answer = $"Resposta valida {index + 1}"
        });
        var validResponse = await client.PutAsJsonAsync(
            $"/api/projects/{targetProjectId}/questions/answers", new { answers = validAnswers });
        validResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Workflow_should_preserve_single_document_and_log_after_repeated_generation()
    {
        var client = await CreateAuthenticatedClientAsync();
        var projectId = await CreateProjectAsync(client);
        var questions = await GenerateQuestionsAsync(client, projectId);
        var answers = questions.Select((question, index) => new
        {
            questionId = question.Id,
            answer = $"Resposta {index + 1}"
        });
        var answerResponse = await client.PutAsJsonAsync($"/api/projects/{projectId}/questions/answers", new { answers });
        answerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstResponse = await client.PostAsync($"/api/projects/{projectId}/generate-document", content: null);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstDocument = await firstResponse.Content.ReadFromJsonAsync<DocumentContract>();

        var repeatedResponse = await client.PostAsync($"/api/projects/{projectId}/generate-document", content: null);

        repeatedResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await repeatedResponse.Content.ReadFromJsonAsync<ProblemDetailsContract>();
        problem.Should().NotBeNull();
        problem!.Extensions["code"].GetString().Should().Be("projects.invalid_status_for_document_generation");
        var getResponse = await client.GetAsync($"/api/projects/{projectId}/document");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var persistedDocument = await getResponse.Content.ReadFromJsonAsync<DocumentContract>();
        persistedDocument!.Overview.Should().Be(firstDocument!.Overview);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SpecPilotDbContext>();
        (await context.ProjectDocuments.AsNoTracking().CountAsync(x => x.ProjectId == projectId)).Should().Be(1);
        (await context.AiInteractionLogs.AsNoTracking().CountAsync(
            x => x.ProjectId == projectId && x.InteractionType == "GenerateProjectDocument")).Should().Be(1);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Pessoa de teste",
            email = $"prompt-{Guid.NewGuid():N}@example.com",
            password = "12345678"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var auth = await response.Content.ReadFromJsonAsync<AuthContract>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    private static async Task<Guid> CreateProjectAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/projects", new
        {
            name = "Projeto do experimento",
            initialDescription = "Ideia inicial para refinamento.",
            goal = "Documentar a ideia.",
            targetAudience = "Estudantes"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ProjectContract>())!.Id;
    }

    private static async Task<List<QuestionContract>> GenerateQuestionsAsync(HttpClient client, Guid projectId)
    {
        var generateResponse = await client.PostAsync($"/api/projects/{projectId}/generate-questions", content: null);
        generateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getResponse = await client.GetAsync($"/api/projects/{projectId}/questions");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await getResponse.Content.ReadFromJsonAsync<QuestionsContract>())!.Questions;
    }

    private sealed class AuthContract
    {
        public string Token { get; set; } = string.Empty;
    }

    private sealed class ProjectContract
    {
        public Guid Id { get; set; }
    }

    private sealed class QuestionsContract
    {
        public Guid ProjectId { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<QuestionContract> Questions { get; set; } = [];
    }

    private sealed class QuestionContract
    {
        public Guid Id { get; set; }
        public string? Answer { get; set; }
    }

    private sealed class DocumentContract
    {
        public string Overview { get; set; } = string.Empty;
    }
}
