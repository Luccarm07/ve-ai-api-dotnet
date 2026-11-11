using VeAI.Domain.Enums;
using VeAI.Domain.Modelos;

namespace VeAI.Domain.Regras;

/// <summary>
/// Decide se o app deve perguntar "Quer avisar a rede de apoio?": só para barreira estrutural
/// com confiança acima do mínimo (0,7 por padrão). Perigos momentâneos nunca viram relato.
/// </summary>
public static class RegraSugestaoRelato
{
    public const double ConfiancaMinimaPadrao = 0.7;

    public static SugestaoRelato? Avaliar(Barreira? barreira, double confiancaMinima = ConfiancaMinimaPadrao)
    {
        if (barreira is null || !barreira.Detectada || barreira.Tipo is null || barreira.Confianca < confiancaMinima)
            return null;

        var tipo = barreira.Tipo.Value;
        return new SugestaoRelato(tipo, barreira.Gravidade ?? Gravidade.MEDIA, Pergunta(tipo));
    }

    public static string Pergunta(TipoRelato tipo)
    {
        var oQue = tipo == TipoRelato.OUTRO ? "uma barreira no caminho" : tipo.Rotulo();
        return $"Parece haver {oQue}. Quer avisar a rede de apoio?";
    }
}
