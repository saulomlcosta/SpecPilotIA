# Prompt B, versão 2: invariantes do fluxo e persistência

## Contexto

Use `docs/02-mvp-scope.md`, `docs/07-api-contracts.md` e os testes atuais. O fluxo usa API autenticada, `FakeAiService` e banco em memória na suíte de integração.

## Objetivo

Gere sequências de chamadas HTTP que tentem violar estas invariantes do MVP:

1. Um projeto só aceita respostas para as suas próprias perguntas.
2. Uma requisição de respostas rejeitada não altera respostas nem `ProjectStatus`.
3. Um projeto tem no máximo um documento gerado e um log de geração bem-sucedida para a primeira solicitação.

## Formato esperado

Para cada caso, informe pré-condições, sequência HTTP, código e `ProblemDetails` esperado, estado que deve ser consultado após a chamada e motivo pelo qual o caso não duplica um teste existente.

## Restrições

- Use os IDs reais retornados pela API para montar os dados de teste.
- Verifique o resultado pela API e, quando necessário, por consulta `AsNoTracking()` ao banco.
- Não ajuste a asserção para aceitar um defeito observado. Registre a divergência entre regra e implementação.
- Não pressuponha garantias de concorrência ou transações do PostgreSQL: esta suíte usa EF Core InMemory.
- Não implemente novas funcionalidades nem dependa de OpenAI real.
