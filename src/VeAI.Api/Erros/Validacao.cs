using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace VeAI.Api.Erros;

/// <summary>
/// Filtro de endpoint que valida o corpo com DataAnnotations antes do handler rodar.
/// Inválido → 400 "Confira os dados enviados." com a lista de campos (formato da API Java).
/// </summary>
public sealed class ValidacaoFiltro<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var corpo = context.Arguments.OfType<T>().FirstOrDefault();
        if (corpo is null)
            return ErroResposta.Resultado(400, "Envie os dados no corpo da requisição.");

        var erros = new List<ValidationResult>();
        if (Validator.TryValidateObject(corpo, new ValidationContext(corpo), erros, validateAllProperties: true))
            return await next(context);

        var campos = erros
            .SelectMany(e => (e.MemberNames.Any() ? e.MemberNames : [""]).Select(m =>
                new CampoErro(JsonNamingPolicy.CamelCase.ConvertName(m), e.ErrorMessage ?? "Valor inválido.")))
            .ToList();
        return ErroResposta.Resultado(400, "Confira os dados enviados.", campos);
    }
}

public static class EndpointExtensions
{
    /// <summary>Valida o corpo do tipo T e documenta o 400 no Swagger.</summary>
    public static RouteHandlerBuilder ComValidacao<T>(this RouteHandlerBuilder builder) where T : class =>
        builder.AddEndpointFilter<ValidacaoFiltro<T>>().ProducesErro(StatusCodes.Status400BadRequest);

    /// <summary>Documenta no Swagger uma resposta de erro no formato ErroResposta.</summary>
    public static RouteHandlerBuilder ProducesErro(this RouteHandlerBuilder builder, params int[] status)
    {
        foreach (var s in status)
            builder.Produces<ErroResposta>(s, "application/json");
        return builder;
    }
}
