using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VeAI.Application.Abstracoes;
using VeAI.Application.Servicos;
using VeAI.Domain.Enums;
using VeAI.Domain.Eventos;

namespace VeAI.Infrastructure.Mensageria;

/// <summary>Configuração do RabbitMQ (seção "Rabbit"). Sem Url, a mensageria roda em memória.</summary>
public sealed class RabbitOptions
{
    public const string Secao = "Rabbit";

    /// <summary>Ex.: amqp://veai:veai@localhost:5672 (fica só no .env).</summary>
    public string? Url { get; set; }

    public bool Ativo => !string.IsNullOrWhiteSpace(Url);
}

/// <summary>
/// Topologia (a mesma da API Java, para as duas poderem usar o mesmo broker):
/// veai.relatos (topic) --relato.*--> veai.notificacoes --falha--> veai.relatos.dlx --> veai.notificacoes.dlq
/// </summary>
public static class Topologia
{
    public const string Exchange = "veai.relatos";
    public const string FilaNotificacoes = "veai.notificacoes";
    public const string Dlx = "veai.relatos.dlx";
    public const string Dlq = "veai.notificacoes.dlq";
    public const string Binding = "relato.*";
}

/// <summary>Formato da mensagem na fila: JSON legível no painel do RabbitMQ, igual ao da API Java.</summary>
public sealed record MensagemEvento(TipoEvento Tipo, long RelatoId, long UsuarioId, StatusRelato? StatusAnterior,
                                    StatusRelato? StatusNovo, DateTime OcorridoEm)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public static byte[] Serializar(EventoRelato e) =>
        JsonSerializer.SerializeToUtf8Bytes(new MensagemEvento(e.Tipo, e.RelatoId, e.UsuarioId, e.StatusAnterior, e.StatusNovo, e.OcorridoEm), Json);

    public static EventoRelato Desserializar(ReadOnlySpan<byte> corpo)
    {
        var m = JsonSerializer.Deserialize<MensagemEvento>(corpo, Json) ?? throw new JsonException("Mensagem vazia");
        return new EventoRelato(m.Tipo, m.RelatoId, m.UsuarioId, m.StatusAnterior, m.StatusNovo, m.OcorridoEm);
    }
}

/// <summary>
/// Processa um evento recebido: gera as notificações. Até 3 tentativas com espera crescente (200, 400, 600 ms);
/// se ainda falhar, devolve false e quem consome manda a mensagem para a dead-letter queue.
/// </summary>
public sealed class ProcessadorEventos(IServiceScopeFactory escopos, ILogger<ProcessadorEventos> log)
{
    public const int Tentativas = 3;

    public async Task<bool> ProcessarAsync(EventoRelato evento, CancellationToken ct = default)
    {
        for (var tentativa = 1; tentativa <= Tentativas; tentativa++)
        {
            try
            {
                using var escopo = escopos.CreateScope();
                var criadas = await escopo.ServiceProvider.GetRequiredService<NotificacaoServico>().GerarAsync(evento, ct);
                log.LogInformation("{RoutingKey} do relato {RelatoId}: {Criadas} notificação(ões) criada(s)",
                    evento.RoutingKey, evento.RelatoId, criadas);
                return true;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                log.LogWarning("Tentativa {Tentativa}/{Total} falhou para {RoutingKey} do relato {RelatoId}: {Erro}",
                    tentativa, Tentativas, evento.RoutingKey, evento.RelatoId, e.Message);
                if (tentativa < Tentativas)
                    await Task.Delay(200 * tentativa, ct);
            }
        }
        log.LogError("Evento {RoutingKey} do relato {RelatoId} enviado para a DLQ {Dlq}", evento.RoutingKey, evento.RelatoId, Topologia.Dlq);
        return false;
    }
}

// ── Modo em memória (sem RabbitMQ configurado): mesma lógica, fila dentro do processo ──────────────

/// <summary>Fila em memória usada quando Rabbit__Url não está configurado (desenvolvimento e testes).</summary>
public sealed class FilaEventosMemoria
{
    public Channel<EventoRelato> Canal { get; } = Channel.CreateUnbounded<EventoRelato>();
    /// <summary>Eventos que falharam nas 3 tentativas (o equivalente à DLQ).</summary>
    public List<EventoRelato> DeadLetter { get; } = [];
}

public sealed class PublicadorEventosEmMemoria(FilaEventosMemoria fila) : IPublicadorEventos
{
    public Task PublicarAsync(EventoRelato evento, CancellationToken ct = default) => fila.Canal.Writer.WriteAsync(evento, ct).AsTask();
}

internal sealed class ConsumidorEmMemoria(FilaEventosMemoria fila, ProcessadorEventos processador) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var evento in fila.Canal.Reader.ReadAllAsync(stoppingToken))
            if (!await processador.ProcessarAsync(evento, stoppingToken))
                lock (fila.DeadLetter)
                    fila.DeadLetter.Add(evento);
    }
}
