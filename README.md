# Vê AI · API .NET (Minimal APIs)

Global Solution 2026/2 · FIAP 2TDSPX · **Advanced Business Development with .NET**

O **Vê AI** é um assistente em português para pessoas com deficiência visual: a pessoa fotografa o caminho e a IA descreve o ambiente por áudio, **com os perigos primeiro**. Quando aparece uma barreira de acessibilidade (buraco, calçada obstruída, rampa irregular...), o cidadão envia um **relato** com GPS ao gestor da região, que faz a triagem e o encaminha ao órgão público.

Esta API implementa, em .NET, o mesmo contrato da [API Java](https://github.com/lgaxd/ve-ai-api-java), consumida pelo app mobile.

| Integrante | RM |
|---|---:|
| Lucas Grillo Alcântara | 561413 |
| Pietro Ferreira Gomes Abrahamian | 561469 |
| Pedro Peres Benitez | 561792 |
| Lucca Ramos Mussumecci | 562027 |

> README em construção: será completado ao longo das etapas.

## Estrutura

```
src/
  VeAI.Domain/          entidades, enums e regras de negócio puras
  VeAI.Application/     DTOs, interfaces e serviços de aplicação
  VeAI.Infrastructure/  EF Core (Oracle), segurança, IA, mensageria, health checks
  VeAI.Api/             Minimal APIs, Swagger, rate limiting, idempotência
tests/
  VeAI.Tests/           xUnit: unitários e de integração
```

## Como rodar (provisório)

Requisito: [.NET SDK 10](https://dotnet.microsoft.com/download).

```bash
dotnet build
dotnet test
dotnet run --project src/VeAI.Api
```

- Swagger: http://localhost:5046/swagger
- Saúde: http://localhost:5046/health
