using Microsoft.AspNetCore.WebUtilities;

namespace VeAI.Api.Erros;

/// <summary>Campo inválido e o motivo, para o app ler em voz alta.</summary>
public sealed record CampoErro(string Campo, string Mensagem);

/// <summary>Formato único de erro da API, igual ao da API Java: {timestamp, status, erro, mensagem, campos[]}.</summary>
public sealed record ErroResposta(DateTime Timestamp, int Status, string Erro, string Mensagem, IReadOnlyList<CampoErro> Campos)
{
    public static ErroResposta De(int status, string mensagem, IReadOnlyList<CampoErro>? campos = null) =>
        new(DateTime.UtcNow, status, Frase(status), mensagem, campos ?? []);

    /// <summary>Mesmos textos do Spring (Java) para os códigos que mudaram de nome na RFC 9110.</summary>
    private static string Frase(int status) => status switch
    {
        413 => "Content Too Large",
        422 => "Unprocessable Content",
        _ => ReasonPhrases.GetReasonPhrase(status)
    };

    public static IResult Resultado(int status, string mensagem, IReadOnlyList<CampoErro>? campos = null) =>
        Results.Json(De(status, mensagem, campos), statusCode: status);

    public static Task EscreverAsync(HttpContext context, int status, string mensagem)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(De(status, mensagem));
    }
}
