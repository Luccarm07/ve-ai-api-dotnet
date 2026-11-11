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

/// <summary>
/// Saída estruturada do modelo de visão. Normalizada garante listas e barreira não nulas
/// e, sem resposta sobre novidade, assume true: na dúvida, o app fala.
/// </summary>
public sealed record DescricaoIA(IReadOnlyList<Perigo>? Perigos, string? Resumo, string? DescricaoCompleta, bool? Novidade,
                                 IReadOnlyList<string>? Objetos, string? TextoLido, Barreira? Barreira)
{
    public DescricaoIA Normalizada() => new(
        Perigos ?? [], Resumo ?? "", DescricaoCompleta ?? "", Novidade ?? true, Objetos ?? [], TextoLido, Barreira ?? Barreira.Nenhuma());
}

/// <summary>
/// Modo automático: a foto anterior da caminhada, já descrita. Com ela o modelo fala só o que mudou
/// (ou confirma em uma frase) e responde se há novidade.
/// </summary>
public sealed record ContextoAnterior(string? DescricaoCompleta, string Resumo, IReadOnlyList<Perigo> Perigos, long SegundosAtras);

/// <summary>Foto recebida do app. Vive só na memória durante a chamada: nunca vai para banco, disco ou log.</summary>
public sealed record ImagemRecebida(byte[] Bytes, string Tipo)
{
    public override string ToString() => $"ImagemRecebida[{Tipo}, {Bytes.Length} bytes]";
}
