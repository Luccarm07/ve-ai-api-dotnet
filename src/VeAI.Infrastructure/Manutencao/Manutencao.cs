using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VeAI.Application.Servicos;

namespace VeAI.Infrastructure.Manutencao;

/// <summary>
/// Tarefas periódicas (equivalem aos @Scheduled da API Java):
/// - ao subir e de hora em hora: recalcula a prioridade dos relatos abertos ("dias em aberto" mudam com o tempo);
/// - de hora em hora: apaga as Idempotency-Key vencidas (mais de 24 h).
/// </summary>
internal sealed class ManutencaoPeriodica(IServiceScopeFactory escopos, ILogger<ManutencaoPeriodica> log) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Intervalo);
        do
        {
            await ExecutarUmaVezAsync(stoppingToken);
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ExecutarUmaVezAsync(CancellationToken ct)
    {
        try
        {
            using var escopo = escopos.CreateScope();
            var recalculados = await escopo.ServiceProvider.GetRequiredService<RecalculoPrioridadeServico>().RecalcularAsync(ct);
            var apagadas = await escopo.ServiceProvider.GetRequiredService<IdempotenciaServico>().LimparVencidasAsync(ct);
            log.LogInformation("Manutenção: prioridade recalculada em {Relatos} relatos; {Chaves} chaves de idempotência vencidas removidas",
                recalculados, apagadas);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // Banco fora do ar não derruba a API: tenta de novo no próximo ciclo (o /health mostra o problema).
            log.LogWarning("Manutenção periódica falhou: {Erro}", e.Message);
        }
    }
}
