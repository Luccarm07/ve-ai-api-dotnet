using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace VeAI.Api.Saude;

/// <summary>Escreve o resultado dos HealthChecks em JSON: status geral e situação de cada dependência.</summary>
public static class RespostaSaude
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static Task Escrever(HttpContext context, HealthReport relatorio)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var corpo = new
        {
            status = relatorio.Status.ToString(),
            duracaoMs = Math.Round(relatorio.TotalDuration.TotalMilliseconds, 1),
            verificacoes = relatorio.Entries.Select(e => new
            {
                nome = e.Key,
                status = e.Value.Status.ToString(),
                descricao = e.Value.Description ?? e.Value.Exception?.Message,
                duracaoMs = Math.Round(e.Value.Duration.TotalMilliseconds, 1)
            })
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(corpo, Json));
    }
}
