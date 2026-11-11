using VeAI.Domain.Enums;

namespace VeAI.Domain.Eventos;

public enum TipoEvento
{
    CRIADO,
    APOIADO,
    STATUS
}

/// <summary>
/// Evento de domínio publicado depois do commit e enviado ao RabbitMQ (exchange veai.relatos).
/// UsuarioId é quem causou o evento: autor, apoiador ou gestor. Mesmo formato da API Java.
/// </summary>
public sealed record EventoRelato(TipoEvento Tipo, long RelatoId, long UsuarioId, StatusRelato? StatusAnterior,
                                  StatusRelato? StatusNovo, DateTime OcorridoEm)
{
    /// <summary>Routing key no RabbitMQ: relato.criado, relato.apoiado ou relato.status.</summary>
    public string RoutingKey => Tipo switch
    {
        TipoEvento.CRIADO => "relato.criado",
        TipoEvento.APOIADO => "relato.apoiado",
        _ => "relato.status"
    };

    public static EventoRelato Criado(long relatoId, long autorId, DateTime agora) =>
        new(TipoEvento.CRIADO, relatoId, autorId, null, StatusRelato.ABERTO, agora);

    public static EventoRelato Apoiado(long relatoId, long apoiadorId, DateTime agora) =>
        new(TipoEvento.APOIADO, relatoId, apoiadorId, null, null, agora);

    public static EventoRelato Status(long relatoId, long gestorId, StatusRelato anterior, StatusRelato novo, DateTime agora) =>
        new(TipoEvento.STATUS, relatoId, gestorId, anterior, novo, agora);
}
