using VeAI.Domain.Enums;

namespace VeAI.Domain.Entities;

/// <summary>"+1": outro cidadão confirmou a mesma barreira. Único por relato e usuário. Tabela APOIO_RELATO.</summary>
public class ApoioRelato
{
    public long Id { get; private set; }
    public long RelatoId { get; private set; }
    public long UsuarioId { get; private set; }
    public DateTime CriadoEm { get; private set; }

    private ApoioRelato() { }

    public ApoioRelato(long relatoId, long usuarioId, DateTime agora)
    {
        RelatoId = relatoId;
        UsuarioId = usuarioId;
        CriadoEm = agora;
    }
}

/// <summary>Histórico de status: qual gestor mudou, de quê para quê e com qual comentário. Tabela ATUALIZACAO_RELATO.</summary>
public class AtualizacaoRelato
{
    public long Id { get; private set; }
    public long RelatoId { get; private set; }
    public long GestorId { get; private set; }
    public Usuario? Gestor { get; private set; }
    public StatusRelato StatusAnterior { get; private set; }
    public StatusRelato StatusNovo { get; private set; }
    public string? Comentario { get; private set; }
    public DateTime CriadoEm { get; private set; }

    private AtualizacaoRelato() { }

    public AtualizacaoRelato(long relatoId, long gestorId, StatusRelato anterior, StatusRelato novo,
                             string? comentario, DateTime agora)
    {
        RelatoId = relatoId;
        GestorId = gestorId;
        StatusAnterior = anterior;
        StatusNovo = novo;
        Comentario = string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim();
        CriadoEm = agora;
    }
}

/// <summary>Mensagem curta, gerada a partir dos eventos do RabbitMQ e lida em voz alta pelo app. Tabela NOTIFICACAO.</summary>
public class Notificacao
{
    public const int TamanhoMaximoMensagem = 300;

    public long Id { get; private set; }
    public long UsuarioId { get; private set; }
    public long RelatoId { get; private set; }
    public string Mensagem { get; private set; } = null!;
    public bool Lida { get; private set; }
    public DateTime CriadoEm { get; private set; }

    private Notificacao() { }

    public Notificacao(long usuarioId, long relatoId, string mensagem, DateTime agora)
    {
        UsuarioId = usuarioId;
        RelatoId = relatoId;
        Mensagem = mensagem.Length <= TamanhoMaximoMensagem ? mensagem : mensagem[..(TamanhoMaximoMensagem - 1)] + "…";
        CriadoEm = agora;
    }

    public void MarcarComoLida() => Lida = true;
}

/// <summary>
/// Resposta guardada de um POST /relatos com Idempotency-Key (tabela IDEMPOTENCIA, V3):
/// repetir a mesma chave devolve a mesma resposta, sem criar outro relato nem outro apoio.
/// </summary>
public class RegistroIdempotencia
{
    public static readonly TimeSpan Validade = TimeSpan.FromHours(24);

    public long Id { get; private set; }
    public long UsuarioId { get; private set; }
    public string Chave { get; private set; } = null!;
    /// <summary>SHA-256 do corpo: a mesma chave com outro conteúdo é recusada (422).</summary>
    public string HashRequisicao { get; private set; } = null!;
    public int StatusHttp { get; private set; }
    public string CorpoResposta { get; private set; } = null!;
    public DateTime CriadoEm { get; private set; }

    private RegistroIdempotencia() { }

    public RegistroIdempotencia(long usuarioId, string chave, string hashRequisicao, int statusHttp,
                                string corpoResposta, DateTime agora)
    {
        UsuarioId = usuarioId;
        Chave = chave;
        HashRequisicao = hashRequisicao;
        StatusHttp = statusHttp;
        CorpoResposta = corpoResposta;
        CriadoEm = agora;
    }

    public bool Expirado(DateTime agora) => agora - CriadoEm > Validade;

    public bool MesmaRequisicao(string hash) => string.Equals(HashRequisicao, hash, StringComparison.Ordinal);
}
