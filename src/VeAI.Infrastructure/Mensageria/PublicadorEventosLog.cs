using Microsoft.Extensions.Logging;
using VeAI.Application.Abstracoes;
using VeAI.Domain.Eventos;

namespace VeAI.Infrastructure.Mensageria;

/// <summary>Publicador provisório: registra o evento no log. Na etapa da mensageria vira RabbitMQ.</summary>
internal sealed class PublicadorEventosLog(ILogger<PublicadorEventosLog> log) : IPublicadorEventos
{
    public Task PublicarAsync(EventoRelato evento, CancellationToken ct = default)
    {
        log.LogInformation("Evento {RoutingKey}: relato {RelatoId} por usuário {UsuarioId}", evento.RoutingKey, evento.RelatoId, evento.UsuarioId);
        return Task.CompletedTask;
    }
}
