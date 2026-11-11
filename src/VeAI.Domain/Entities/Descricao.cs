using VeAI.Domain.Enums;
using VeAI.Domain.Modelos;

namespace VeAI.Domain.Entities;

/// <summary>Descrição gerada pela IA. Só o texto é guardado: a imagem nunca é armazenada. Tabela DESCRICAO.</summary>
public class Descricao
{
    public long Id { get; private set; }
    public long UsuarioId { get; private set; }
    public Usuario? Usuario { get; private set; }
    public ModoDescricao Modo { get; private set; }
    public string Resumo { get; private set; } = null!;
    public string? DescricaoCompleta { get; private set; }
    /// <summary>Gravado como JSON (CLOB com CHECK IS JSON no Oracle).</summary>
    public List<Perigo> Perigos { get; private set; } = [];
    public List<string> Objetos { get; private set; } = [];
    public bool BarreiraDetectada { get; private set; }
    public TipoRelato? BarreiraTipo { get; private set; }
    public int LatenciaMs { get; private set; }
    /// <summary>true = útil, false = não útil, null = ainda não avaliada.</summary>
    public bool? Util { get; private set; }
    public DateTime CriadoEm { get; private set; }

    private Descricao() { }

    public Descricao(long usuarioId, ModoDescricao modo, string resumo, string? descricaoCompleta,
                     IEnumerable<Perigo> perigos, IEnumerable<string> objetos,
                     bool barreiraDetectada, TipoRelato? barreiraTipo, int latenciaMs, DateTime agora)
    {
        if (barreiraDetectada && barreiraTipo is null)
            throw Excecoes.RegraNegocioException.Regra("Barreira detectada precisa de um tipo.");

        UsuarioId = usuarioId;
        Modo = modo;
        Resumo = resumo;
        DescricaoCompleta = descricaoCompleta;
        Perigos = perigos.ToList();
        Objetos = objetos.ToList();
        BarreiraDetectada = barreiraDetectada;
        BarreiraTipo = barreiraDetectada ? barreiraTipo : null;
        LatenciaMs = Math.Max(0, latenciaMs);
        CriadoEm = agora;
    }

    public void Avaliar(bool util) => Util = util;

    public bool EhDo(long usuarioId) => UsuarioId == usuarioId;
}
