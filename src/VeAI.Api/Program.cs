using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

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

// HealthChecks: "self" indica que o processo está vivo. Banco, RabbitMQ e OpenAI entram nas próximas etapas.
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("API no ar"), tags: ["live"]);

var app = builder.Build();

app.MapOpenApi();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "Vê AI · API .NET v1");
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "Vê AI · Swagger";
});

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = check => check.Tags.Contains("live") });

app.Run();

/// <summary>Visível para os testes de integração (WebApplicationFactory).</summary>
public partial class Program;
