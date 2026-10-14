# 16 - Experimento de prompts para testes de integração

## Cenário e objetivo

Este exercício usa o fluxo real do MVP do SpecPilot AI: autenticação, criação de projeto, geração de perguntas, envio de respostas e geração do documento inicial. As chamadas atravessam a API ASP.NET Core, os casos de uso, o `FakeAiService` e a persistência usada pela suíte. O objetivo foi comparar dois modos de pedir casos de teste a uma LLM, sem ampliar as funcionalidades do produto.

A LLM utilizada foi o Codex nesta sessão de desenvolvimento. Os prompts e os casos resultantes foram revisados contra [o contrato da API](07-api-contracts.md), [o escopo do MVP](02-mvp-scope.md) e a suíte já existente. Os testes gerados estão em [PromptGeneratedIntegrationTests.cs](../tests/backend/SpecPilot.IntegrationTests/Projects/PromptGeneratedIntegrationTests.cs).

## Prompt A: derivação pelo contrato HTTP

O [prompt A v1](../prompts/tests/integration-contract-v1.md) pediu casos para as rotas de perguntas e documento. A primeira lista continha o caminho feliz de resposta, resposta incompleta, documento antes de responder e consulta do documento. São casos válidos, porém já cobertos pelos testes existentes. A formulação ampla favoreceu cobertura funcional conhecida e pouca novidade.

O [prompt A v2](../prompts/tests/integration-contract-v2.md) acrescentou a leitura dos testes existentes, exigiu chamadas pela borda HTTP, excluiu duplicações e pediu verificação de `ProblemDetails`. Dessa revisão saíram dois casos implementados:

| Caso | Integrações verificadas | Asserção principal |
| --- | --- | --- |
| `Contract_should_return_persisted_answers_in_question_order` | autenticação, envio de respostas e consulta posterior pela API | IDs e respostas retornam na ordem das perguntas, com status `QuestionsAnswered` |
| `Contract_should_reject_document_generation_without_token` | middleware JWT e rota de geração | `401` em `application/problem+json`, com código `auth.unauthorized` |

O prompt orientado ao contrato ajudou a definir respostas HTTP precisas. A comparação com a suíte existente foi o ajuste decisivo para evitar repetir casos já testados.

## Prompt B: invariantes do fluxo

O [prompt B v1](../prompts/tests/integration-workflow-v1.md) pediu falhas fora do fluxo ideal. A lista inicial incluiu resposta com ID repetido, resposta com ID de outro projeto, geração de documento repetida e geração simultânea. A sugestão de simultaneidade foi descartada porque a suíte usa EF Core InMemory e não oferece base para concluir sobre concorrência do PostgreSQL.

O [prompt B v2](../prompts/tests/integration-workflow-v2.md) explicitou três invariantes: aceitar apenas perguntas do projeto, não gravar alterações parciais após erro e manter apenas um documento e um log de geração bem-sucedida. Também exigiu inspeção do estado após a resposta HTTP. Dois casos foram implementados:

| Caso | Sequência | Invariante verificada |
| --- | --- | --- |
| `Workflow_should_reject_answers_from_another_project_without_partial_update` | criar dois projetos do mesmo usuário, misturar um ID de pergunta, receber `400`, inspecionar estado e enviar respostas válidas | pergunta de outro projeto é recusada; nenhuma resposta parcial é gravada; o projeto continua utilizável |
| `Workflow_should_preserve_single_document_and_log_after_repeated_generation` | gerar documento, repetir `POST`, consultar documento e banco | a segunda chamada recebe `409`; permanecem um documento e um log de geração |

O prompt orientado a invariantes produziu sequências com mais interações entre componentes e asserções sobre o estado após falhas. A exigência de verificar a persistência tornou os casos mais úteis do que apenas conferir o código HTTP.

## Execução e limites

Comando para reproduzir apenas os quatro casos deste exercício:

```powershell
dotnet test tests/backend/SpecPilot.IntegrationTests/SpecPilot.IntegrationTests.csproj --filter FullyQualifiedName~PromptGeneratedIntegrationTests
```

Na execução local de 14/10/2026, os quatro testes passaram. A suíte completa do backend também passou: 53 testes unitários e 41 de integração. Nenhum defeito foi encontrado nesses casos. O resultado demonstra os contratos e invariantes observados neste ambiente; não deve ser apresentado como prova de ausência de defeitos.

A fábrica de testes configura `FakeAiService` e EF Core InMemory. Portanto, os casos não dependem de internet nem de chave OpenAI e não verificam diferenças de transação ou concorrência do PostgreSQL. A IA ajudou a propor combinações de chamadas e asserções; a seleção exigiu conferência dos contratos, do código existente e dos resultados dos testes.
