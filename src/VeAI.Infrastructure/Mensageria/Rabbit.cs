using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using VeAI.Application.Abstracoes;
using VeAI.Domain.Eventos;

namespace VeAI.Infrastructure.Mensageria;

/// <summary>Conexão única com o RabbitMQ (reconecta sozinha) e declaração da topologia, tudo durável.</summary>
public sealed class ConexaoRabbit(IOptions<RabbitOptions> opcoes, ILogger<ConexaoRabbit> log) : IAsyncDisposable
{
    private readonly SemaphoreSlim _trava = new(1, 1);
    private IConnection? _conexao;
    private IChannel? _canalPublicacao;

    public bool Conectado => _conexao is { IsOpen: true };

    public async Task<IConnection> ConexaoAsync(CancellationToken ct = default)
    {
        if (_conexao is { IsOpen: true } aberta)
            return aberta;

        await _trava.WaitAsync(ct);
        try
        {
            if (_conexao is { IsOpen: true } jaAberta)
                return jaAberta;

            var fabrica = new ConnectionFactory
            {
                Uri = new Uri(opcoes.Value.Url!),
                ClientProvidedName = "ve-ai-api-dotnet",
                AutomaticRecoveryEnabled = true,
                RequestedConnectionTimeout = TimeSpan.FromSeconds(5)
            };
            _conexao = await fabrica.CreateConnectionAsync(ct);
            await using var canal = await _conexao.CreateChannelAsync(cancellationToken: ct);
            await DeclararTopologiaAsync(canal, ct);
            log.LogInformation("RabbitMQ conectado: exchange {Exchange}, fila {Fila}", Topologia.Exchange, Topologia.FilaNotificacoes);
            return _conexao;
        }
        finally
        {
            _trava.Release();
        }
    }

    private static async Task DeclararTopologiaAsync(IChannel canal, CancellationToken ct)
    {
        await canal.ExchangeDeclareAsync(Topologia.Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);
        await canal.ExchangeDeclareAsync(Topologia.Dlx, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: ct);
        await canal.QueueDeclareAsync(Topologia.FilaNotificacoes, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = Topologia.Dlx,
                ["x-dead-letter-routing-key"] = Topologia.Dlq
            }, cancellationToken: ct);
        await canal.QueueDeclareAsync(Topologia.Dlq, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await canal.QueueBindAsync(Topologia.FilaNotificacoes, Topologia.Exchange, Topologia.Binding, cancellationToken: ct);
        await canal.QueueBindAsync(Topologia.Dlq, Topologia.Dlx, Topologia.Dlq, cancellationToken: ct);
    }

    /// <summary>Publica uma mensagem persistente. O canal de publicação é compartilhado, então uma publicação por vez.</summary>
    public async Task PublicarAsync(string routingKey, byte[] corpo, CancellationToken ct = default)
    {
        var conexao = await ConexaoAsync(ct);
        await _trava.WaitAsync(ct);
        try
        {
            if (_canalPublicacao is not { IsOpen: true })
                _canalPublicacao = await conexao.CreateChannelAsync(cancellationToken: ct);
            var propriedades = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = Guid.NewGuid().ToString(),
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };
            await _canalPublicacao.BasicPublishAsync(Topologia.Exchange, routingKey, mandatory: false, propriedades, corpo, ct);
        }
        finally
        {
            _trava.Release();
        }
    }

    /// <summary>Mensagens esperando na fila e na DLQ (para o /health).</summary>
    public async Task<(uint Fila, uint Dlq)> ContarAsync(CancellationToken ct = default)
    {
        await using var canal = await (await ConexaoAsync(ct)).CreateChannelAsync(cancellationToken: ct);
        var fila = await canal.QueueDeclarePassiveAsync(Topologia.FilaNotificacoes, ct);
        var dlq = await canal.QueueDeclarePassiveAsync(Topologia.Dlq, ct);
        return (fila.MessageCount, dlq.MessageCount);
    }

    public async ValueTask DisposeAsync()
    {
        if (_canalPublicacao is not null)
            await _canalPublicacao.DisposeAsync();
        if (_conexao is not null)
            await _conexao.DisposeAsync();
    }
}

/// <summary>
/// Publica no RabbitMQ (chamado só depois do commit). Se o broker estiver fora, a operação do usuário já foi gravada:
/// a falha fica no log e a requisição não quebra.
/// </summary>
internal sealed class PublicadorEventosRabbit(ConexaoRabbit rabbit, ILogger<PublicadorEventosRabbit> log) : IPublicadorEventos
{
    public async Task PublicarAsync(EventoRelato evento, CancellationToken ct = default)
    {
        try
        {
            await rabbit.PublicarAsync(evento.RoutingKey, MensagemEvento.Serializar(evento), ct);
            log.LogDebug("Evento publicado: {RoutingKey} relato {RelatoId}", evento.RoutingKey, evento.RelatoId);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            log.LogError("Não foi possível publicar {RoutingKey} do relato {RelatoId}: {Erro}", evento.RoutingKey, evento.RelatoId, e.Message);
        }
    }
}

/// <summary>
/// Consome veai.notificacoes com ack manual: sucesso → ack; falhou nas 3 tentativas ou mensagem ilegível →
/// nack sem requeue → a mensagem vai para a dead-letter queue. Se o broker cair, tenta reconectar a cada 5 s.
/// </summary>
internal sealed class ConsumidorRabbit(ConexaoRabbit rabbit, ProcessadorEventos processador, ILogger<ConsumidorRabbit> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var conexao = await rabbit.ConexaoAsync(stoppingToken);
                await using var canal = await conexao.CreateChannelAsync(cancellationToken: stoppingToken);
                await canal.BasicQosAsync(0, prefetchCount: 10, global: false, stoppingToken);

                var consumidor = new AsyncEventingBasicConsumer(canal);
                consumidor.ReceivedAsync += async (_, entrega) =>
                {
                    bool ok;
                    try
                    {
                        ok = await processador.ProcessarAsync(MensagemEvento.Desserializar(entrega.Body.Span), stoppingToken);
                    }
                    catch (System.Text.Json.JsonException e)
                    {
                        log.LogError("Mensagem ilegível enviada para a DLQ: {Erro}", e.Message);
                        ok = false;
                    }
                    if (ok)
                        await canal.BasicAckAsync(entrega.DeliveryTag, multiple: false);
                    else
                        await canal.BasicNackAsync(entrega.DeliveryTag, multiple: false, requeue: false);
                };
                await canal.BasicConsumeAsync(Topologia.FilaNotificacoes, autoAck: false, consumidor, stoppingToken);
                log.LogInformation("Consumindo a fila {Fila}", Topologia.FilaNotificacoes);

                while (canal.IsOpen && !stoppingToken.IsCancellationRequested)
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                log.LogWarning("RabbitMQ indisponível ({Erro}). Nova tentativa em 5 s.", e.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}

/// <summary>
/// Saúde da mensageria: conecta no RabbitMQ e informa quantas mensagens esperam na fila e na DLQ.
/// Broker fora deixa a API "Degraded" (as operações seguem; só as notificações atrasam).
/// </summary>
internal sealed class MensageriaHealthCheck(IOptions<RabbitOptions> opcoes, IServiceProvider servicos) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        if (!opcoes.Value.Ativo)
            return HealthCheckResult.Healthy("Mensageria em memória: RabbitMQ não configurado (Rabbit__Url).");
        try
        {
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limite.CancelAfter(TimeSpan.FromSeconds(5));
            var (fila, dlq) = await ((ConexaoRabbit)servicos.GetService(typeof(ConexaoRabbit))!).ContarAsync(limite.Token);
            return HealthCheckResult.Healthy($"RabbitMQ conectado. Fila {Topologia.FilaNotificacoes}: {fila} mensagem(ns); DLQ: {dlq}.");
        }
        catch (Exception e)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, $"RabbitMQ inacessível: {e.Message}");
        }
    }
}
