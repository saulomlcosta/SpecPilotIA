# Prompt A, versão 1: contrato HTTP

## Contexto

O SpecPilot AI é um MVP com API ASP.NET Core. Um usuário autenticado cria um projeto, gera perguntas de refinamento, responde e gera um documento. A IA dos testes deve ser `FakeAiService`.

## Objetivo

Sugira casos de teste de integração para as rotas de perguntas e documento a partir de `docs/07-api-contracts.md`.

## Formato esperado

Para cada caso, informe preparação, chamadas HTTP e resultado esperado.

## Restrições

Use apenas o fluxo do MVP e não dependa da OpenAI real.
