# Prompt A, versão 2: contrato HTTP com lacunas de cobertura

## Contexto

Considere `docs/07-api-contracts.md` e os testes já existentes em `tests/backend/SpecPilot.IntegrationTests`. O cenário é o fluxo autenticado de perguntas e documento do SpecPilot AI. A suíte usa `WebApplicationFactory`, EF Core InMemory e `FakeAiService`.

## Objetivo

Gere casos de integração novos, derivados dos contratos HTTP, que atravessem a borda da API. Priorize comportamentos ainda não cobertos, como consultar respostas depois de gravá-las e proteger a geração de documento sem token.

## Formato esperado

Entregue uma tabela com: rota e método, preparação, resposta HTTP esperada, campos ou código de `ProblemDetails` a verificar e teste existente que deixaria de ser duplicado.

## Restrições

- Não invente campos, códigos HTTP nem regras ausentes do contrato ou da implementação.
- Não chame handlers diretamente; use `HttpClient` da fábrica de teste.
- Não repita o caminho feliz completo nem os casos de erro já presentes na suíte.
- Use apenas `FakeAiService`, sem internet ou chave externa.
