using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using VeAI.Api.Configuracao;
using VeAI.Api.Endpoints;
using VeAI.Api.Erros;
using VeAI.Api.Saude;
using VeAI.Api.Seguranca;
using VeAI.Application;
using VeAI.Infrastructure;

// Carrega o .env (se existir) antes de montar a configuração. Segredos ficam só nele, nunca no git.
DotEnv.Carregar();

var builder = WebApplication.CreateBuilder(args);

// ── Serviços ────────────────────────────────────────────────────────────────
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddAutenticacaoJwt(builder.Configuration)
    .AddLimitesRequisicao(builder.Configuration)
    .AddDocumentacaoOpenApi();

// JSON: camelCase (padrão) e enums como texto ("CIDADAO", "ABERTO"...), igual à API Java.
builder.Services.Configure<JsonOptions>(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Erros de binding (JSON malformado, tipo errado) viram exceção e passam pelo TratadorErros.
builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);
builder.Services.AddExceptionHandler<TratadorErros>();
builder.Services.AddProblemDetails();

// "api" indica que o processo está vivo. O banco (Oracle) é registrado pela Infrastructure.
builder.Services.AddHealthChecks()
    .AddCheck("api", () => HealthCheckResult.Healthy("API no ar"), tags: ["live"]);

var app = builder.Build();

if (AutenticacaoJwt.ChaveTemporaria)
    app.Logger.LogWarning("Jwt__Segredo não configurado: usando uma chave temporária. Os tokens deixam de valer ao reiniciar.");

// ── Pipeline ────────────────────────────────────────────────────────────────
app.UseExceptionHandler();
app.UseStatusCodePages(async contexto =>
{
    // 404/405 sem corpo (rota inexistente, método errado) no mesmo formato de erro.
    var resposta = contexto.HttpContext.Response;
    if (resposta.HasStarted || resposta.ContentLength > 0 || resposta.ContentType is not null)
        return;
    var mensagem = resposta.StatusCode switch
    {
        404 => "Endereço não encontrado.",
        405 => "Operação não permitida neste endereço.",
        _ => "Não foi possível atender a requisição."
    };
    await ErroResposta.EscreverAsync(contexto.HttpContext, resposta.StatusCode, mensagem);
});

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "Vê AI · API .NET v1");
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "Vê AI · Swagger";
    options.EnablePersistAuthorization();
});

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter(); // depois da autenticação: o limite é por usuário logado

app.MapOpenApi().AllowAnonymous();
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription().AllowAnonymous();

// ── Endpoints (Minimal APIs) ────────────────────────────────────────────────
app.MapAuth();
app.MapUsuarios();
app.MapRegioes();
app.MapRelatos();
app.MapGestor();
app.MapDescricoes();

// /health: API + banco + serviços externos · /health/live: só o processo · /health/ready: dependências.
app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = RespostaSaude.Escrever }).AllowAnonymous().DisableRateLimiting();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = c => c.Tags.Contains("live"), ResponseWriter = RespostaSaude.Escrever
}).AllowAnonymous().DisableRateLimiting();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = c => c.Tags.Contains("ready"), ResponseWriter = RespostaSaude.Escrever
}).AllowAnonymous().DisableRateLimiting();

app.Run();

/// <summary>Visível para os testes de integração (WebApplicationFactory).</summary>
public partial class Program;
