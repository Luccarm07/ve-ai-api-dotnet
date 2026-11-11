using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using VeAI.Api.Configuracao;
using VeAI.Api.Saude;
using VeAI.Infrastructure;

// Carrega o .env (se existir) antes de montar a configuração. Segredos ficam só nele, nunca no git.
DotEnv.Carregar();

var builder = WebApplication.CreateBuilder(args);

// OpenAPI nativo do ASP.NET Core (documento em /openapi/v1.json) + Swagger UI em /swagger.
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Vê AI · API .NET";
        document.Info.Version = "v1";
        document.Info.Description =
            "Assistente para pessoas com deficiência visual: descrição de imagens por IA (perigos primeiro) " +
            "e relatos de barreiras de acessibilidade encaminhados ao gestor da região. Global Solution 2026/2 · FIAP.";
        return Task.CompletedTask;
    });
});

builder.Services.AddInfrastructure(builder.Configuration);

// "self" indica que o processo está vivo. O banco (Oracle) é registrado pela Infrastructure.
builder.Services.AddHealthChecks()
    .AddCheck("api", () => HealthCheckResult.Healthy("API no ar"), tags: ["live"]);

var app = builder.Build();

app.MapOpenApi();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "Vê AI · API .NET v1");
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "Vê AI · Swagger";
});

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

// /health: tudo (API + banco + serviços externos) · /health/live: só o processo · /health/ready: dependências.
app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = RespostaSaude.Escrever });
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = c => c.Tags.Contains("live"), ResponseWriter = RespostaSaude.Escrever
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = c => c.Tags.Contains("ready"), ResponseWriter = RespostaSaude.Escrever
});

app.Run();

/// <summary>Visível para os testes de integração (WebApplicationFactory).</summary>
public partial class Program;
