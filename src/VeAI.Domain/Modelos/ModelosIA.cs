using VeAI.Domain.Enums;

namespace VeAI.Domain.Modelos;

/// <summary>Perigo imediato, falado antes de tudo. Ex.: "degrau para baixo", "à frente", "cerca de 2 metros".</summary>
public sealed record Perigo(string Descricao, string? Direcao, string? DistanciaAprox);

/// <summary>Barreira estrutural identificada pela IA, com a confiança (0 a 1) usada para sugerir um relato.</summary>
public sealed record Barreira(bool Detectada, TipoRelato? Tipo, Gravidade? Gravidade, double Confianca, string? Justificativa)
{
    public static Barreira Nenhuma() => new(false, null, null, 0d, null);
}

/// <summary>O que o app pergunta ao cidadão: "Parece haver ... Quer avisar a rede de apoio?".</summary>
public sealed record SugestaoRelato(TipoRelato Tipo, Gravidade Gravidade, string Pergunta);
