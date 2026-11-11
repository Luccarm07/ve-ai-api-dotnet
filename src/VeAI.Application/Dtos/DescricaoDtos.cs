using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using VeAI.Domain.Entities;
using VeAI.Domain.Enums;
using VeAI.Domain.Modelos;

namespace VeAI.Application.Dtos;

/// <summary>
/// Descrição gerada pela IA. O app fala a descricaoCompleta, que já começa pelos perigos (com posição e distância).
/// "novidade" só vem na resposta do POST; "sugestaoRelato" só quando a IA viu uma barreira com confiança alta.
/// </summary>
public sealed record DescricaoResposta(
    long Id, ModoDescricao Modo, IReadOnlyList<Perigo> Perigos, string Resumo, string? DescricaoCompleta, bool? Novidade,
    IReadOnlyList<string> Objetos, bool BarreiraDetectada, TipoRelato? BarreiraTipo, int LatenciaMs, bool? Util,
    DateTime CriadoEm, SugestaoRelato? SugestaoRelato)
{
    /// <summary>Para o histórico: "novidade" só existe na resposta do POST.</summary>
    public static DescricaoResposta De(Descricao d, bool? novidade = null, SugestaoRelato? sugestao = null) => new(
        d.Id, d.Modo, d.Perigos, d.Resumo, d.DescricaoCompleta, novidade, d.Objetos, d.BarreiraDetectada, d.BarreiraTipo,
        d.LatenciaMs, d.Util, d.CriadoEm, sugestao);
}

public sealed record AvaliacaoRequest
{
    [Required(ErrorMessage = "Informe se a descrição foi útil.")]
    [DefaultValue(true)]
    public bool? Util { get; init; }
}

public sealed record PerguntaResposta(string Resposta);
