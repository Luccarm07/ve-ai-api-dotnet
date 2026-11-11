using System.Net.Http.Headers;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using VeAI.Application.Opcoes;

namespace VeAI.Infrastructure.Ia;

/// <summary>
/// Saúde do serviço externo de IA, sem gastar tokens: consulta GET /v1/models/{modelo} (confere chave e alcance)
/// e guarda o resultado por 60 s. Com o descritor falso, informa que está em modo de demonstração.
/// Falha aqui deixa a API "Degraded" (relatos continuam funcionando), não "Unhealthy".
/// </summary>
internal sealed class IaHealthCheck(IHttpClientFactory fabrica, IOptions<IaOptions> opcoes, TimeProvider relogio) : IHealthCheck
{
    public const string ClienteHttp = "openai-saude";
    private static readonly TimeSpan Cache = TimeSpan.FromSeconds(60);
    private HealthCheckResult? _ultimo;
    private DateTimeOffset _ultimoEm;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var config = opcoes.Value;
        if (!config.OpenAi)
            return HealthCheckResult.Healthy("IA em modo falso: respostas fixas, sem custo (Ia__Provedor=falso).");
        if (string.IsNullOrWhiteSpace(config.ApiKey))
            return new HealthCheckResult(context.Registration.FailureStatus, "OPENAI_API_KEY ausente.");
        if (_ultimo is { } cache && relogio.GetUtcNow() < _ultimoEm + Cache)
            return cache;

        HealthCheckResult resultado;
        try
        {
            using var requisicao = new HttpRequestMessage(HttpMethod.Get, $"/v1/models/{config.Modelo}");
            requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
            using var resposta = await fabrica.CreateClient(ClienteHttp).SendAsync(requisicao, ct);
            resultado = resposta.IsSuccessStatusCode
                ? HealthCheckResult.Healthy($"OpenAI acessível (modelo {config.Modelo}).")
                : new HealthCheckResult(context.Registration.FailureStatus, $"OpenAI respondeu {(int)resposta.StatusCode}.");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            resultado = new HealthCheckResult(context.Registration.FailureStatus, $"OpenAI inacessível: {e.Message}");
        }

        (_ultimo, _ultimoEm) = (resultado, relogio.GetUtcNow());
        return resultado;
    }
}
