using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using VeAI.Domain.Excecoes;

namespace VeAI.Api.Erros;

/// <summary>Converte qualquer exceção no formato ErroResposta, com texto em português (igual à API Java).</summary>
public sealed class TratadorErros(ILogger<TratadorErros> log) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, mensagem) = exception switch
        {
            RegraNegocioException e => (StatusDe(e.Tipo), e.Message),
            BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } =>
                (413, "A foto passa de 5 MB. Envie uma foto menor."),
            BadHttpRequestException { StatusCode: StatusCodes.Status415UnsupportedMediaType } =>
                (415, "Envie a foto como multipart/form-data, no campo imagem."),
            BadHttpRequestException => (400, "Não entendi os dados enviados. Confira o formato e os valores."),
            DbUpdateException => (409, "Esta operação conflita com dados já existentes. Atualize e tente de novo."),
            OperationCanceledException => (499, "Requisição cancelada."),
            _ => (500, "Algo deu errado do nosso lado. Tente de novo em instantes.")
        };

        if (status == 500)
            log.LogError(exception, "Erro inesperado em {Metodo} {Caminho}", context.Request.Method, context.Request.Path);
        else if (exception is DbUpdateException)
            log.LogWarning("Violação de integridade: {Erro}", exception.InnerException?.Message ?? exception.Message);

        await ErroResposta.EscreverAsync(context, status, mensagem);
        return true;
    }

    public static int StatusDe(TipoErro tipo) => tipo switch
    {
        TipoErro.RequisicaoInvalida => StatusCodes.Status400BadRequest,
        TipoErro.NaoAutenticado => StatusCodes.Status401Unauthorized,
        TipoErro.Proibido => StatusCodes.Status403Forbidden,
        TipoErro.NaoEncontrado => StatusCodes.Status404NotFound,
        TipoErro.Conflito => StatusCodes.Status409Conflict,
        TipoErro.ArquivoGrande => StatusCodes.Status413PayloadTooLarge,
        TipoErro.FormatoNaoSuportado => StatusCodes.Status415UnsupportedMediaType,
        TipoErro.ServicoExterno => StatusCodes.Status502BadGateway,
        _ => StatusCodes.Status422UnprocessableEntity
    };
}
