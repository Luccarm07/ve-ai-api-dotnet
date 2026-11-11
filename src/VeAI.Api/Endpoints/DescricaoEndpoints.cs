using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using VeAI.Api.Configuracao;
using VeAI.Api.Erros;
using VeAI.Api.Seguranca;
using VeAI.Application.Dtos;
using VeAI.Application.Servicos;
using VeAI.Domain.Enums;
using VeAI.Domain.Excecoes;

namespace VeAI.Api.Endpoints;

public static class DescricaoEndpoints
{
    /// <summary>Limite do corpo da requisição (a foto em si é limitada a 5 MB pela regra de negócio).</summary>
    private const long CorpoMaximo = 10 * 1024 * 1024;

    public static IEndpointRouteBuilder MapDescricoes(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/descricoes")
            .WithTags("Descrições")
            .RequireAuthorization(AutenticacaoJwt.PoliticaCidadao)
            .ProducesErroGrupo(StatusCodes.Status403Forbidden);

        grupo.MapPost("", async (IFormFile? imagem, [FromQuery] ModoDescricao? modo, [FromQuery] long? anteriorId,
                    HttpRequest request, ClaimsPrincipal user, DescricaoServico servico, CancellationToken ct) =>
            {
                // Como na API Java, modo e anteriorId podem vir na URL ou como campos do formulário.
                var modoFinal = modo ?? LerEnum<ModoDescricao>(request, "modo") ?? ModoDescricao.AMBIENTE;
                var anterior = anteriorId ?? LerLong(request, "anteriorId");
                var resposta = await servico.DescreverAsync(user.UsuarioId(), modoFinal, await LerBytes(imagem, ct), anterior, ct);
                return Results.Created($"/descricoes/{resposta.Id}", resposta);
            })
            .WithName("Descrever")
            .WithSummary("Descreve a foto (perigos primeiro) e sugere relato se houver barreira")
            .WithDescription(
                "Envie a foto (JPEG, PNG ou WebP, até 5 MB) como multipart/form-data no campo **imagem**. A foto nunca é guardada.\n\n" +
                "**Modo manual:** só a foto. **Modo automático** (fotos a cada poucos segundos enquanto a pessoa anda): envie também " +
                "**anteriorId**, o id da resposta anterior. A IA compara as cenas: com novidade, a descricaoCompleta traz só o que mudou; " +
                "sem novidade, é uma confirmação curta. A mesma sugestão de relato não se repete por 2 minutos.\n\n" +
                "**Rate limit:** até 20 fotos por minuto por usuário; além disso, 429 com o header Retry-After.")
            .DisableAntiforgery() // API com JWT no header, sem cookie: não há risco de CSRF
            .WithMetadata(new RequestSizeLimitAttribute(CorpoMaximo))
            .RequireRateLimiting(LimitesRequisicao.PoliticaFotos)
            .Produces<DescricaoResposta>(StatusCodes.Status201Created)
            .ProducesErro(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status413PayloadTooLarge,
                StatusCodes.Status415UnsupportedMediaType, StatusCodes.Status429TooManyRequests, StatusCodes.Status502BadGateway);

        grupo.MapGet("", async (int? page, int? size, ClaimsPrincipal user, DescricaoServico servico, CancellationToken ct) =>
                Results.Ok(await servico.ListarAsync(user.UsuarioId(), page, size, ct)))
            .WithName("HistoricoDescricoes")
            .WithSummary("Histórico de descrições, da mais recente para a mais antiga")
            .Produces<PaginaResposta<DescricaoResposta>>();

        grupo.MapGet("/{id:long}", async (long id, ClaimsPrincipal user, DescricaoServico servico, CancellationToken ct) =>
                Results.Ok(await servico.BuscarAsync(user.UsuarioId(), id, ct)))
            .WithName("BuscarDescricao")
            .WithSummary("Detalhe de uma descrição")
            .Produces<DescricaoResposta>()
            .ProducesErro(StatusCodes.Status404NotFound);

        grupo.MapPatch("/{id:long}/avaliacao", async (long id, AvaliacaoRequest req, ClaimsPrincipal user, DescricaoServico servico,
                    CancellationToken ct) =>
                Results.Ok(await servico.AvaliarAsync(user.UsuarioId(), id, req.Util!.Value, ct)))
            .WithName("AvaliarDescricao")
            .WithSummary("Marca a descrição como útil ou não útil")
            .ComValidacao<AvaliacaoRequest>()
            .Produces<DescricaoResposta>()
            .ProducesErro(StatusCodes.Status404NotFound);

        grupo.MapDelete("/{id:long}", async (long id, ClaimsPrincipal user, DescricaoServico servico, CancellationToken ct) =>
            {
                await servico.ExcluirAsync(user.UsuarioId(), id, ct);
                return Results.NoContent();
            })
            .WithName("ExcluirDescricao")
            .WithSummary("Exclui a descrição do histórico (relatos criados a partir dela continuam existindo)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesErro(StatusCodes.Status404NotFound);

        grupo.MapPost("/{id:long}/pergunta", async (long id, IFormFile? imagem, [FromQuery] string? pergunta, HttpRequest request,
                    ClaimsPrincipal user, DescricaoServico servico, CancellationToken ct) =>
            {
                var texto = pergunta ?? (request.HasFormContentType ? request.Form["pergunta"].ToString() : null);
                return Results.Ok(await servico.PerguntarAsync(user.UsuarioId(), id, texto, await LerBytes(imagem, ct), ct));
            })
            .WithName("PerguntaLivre")
            .WithSummary("Pergunta livre por voz sobre a foto (o app reenvia a imagem; nada é gravado)")
            .WithDescription("Exige perguntaLivreAtiva = true nas preferências (senão 403). Conta no mesmo limite de fotos por minuto.")
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(CorpoMaximo))
            .RequireRateLimiting(LimitesRequisicao.PoliticaFotos)
            .Produces<PerguntaResposta>()
            .ProducesErro(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status413PayloadTooLarge,
                StatusCodes.Status415UnsupportedMediaType, StatusCodes.Status429TooManyRequests, StatusCodes.Status502BadGateway);

        return app;
    }

    /// <summary>Lê a foto para a memória (ela nunca vai para disco nem banco).</summary>
    private static async Task<byte[]> LerBytes(IFormFile? imagem, CancellationToken ct)
    {
        if (imagem is null || imagem.Length == 0)
            return [];
        if (imagem.Length > Domain.Regras.AssinaturaImagem.TamanhoMaximoBytes)
            throw new RegraNegocioException(TipoErro.ArquivoGrande, "A foto passa de 5 MB. Envie uma foto menor.");
        using var memoria = new MemoryStream((int)imagem.Length);
        await imagem.CopyToAsync(memoria, ct);
        return memoria.ToArray();
    }

    private static T? LerEnum<T>(HttpRequest request, string campo) where T : struct, Enum
    {
        if (!request.HasFormContentType || !request.Form.TryGetValue(campo, out var valor) || string.IsNullOrWhiteSpace(valor))
            return null;
        return Enum.TryParse<T>(valor, ignoreCase: true, out var resultado)
            ? resultado
            : throw RegraNegocioException.Invalida($"Valor inválido para {campo}.");
    }

    private static long? LerLong(HttpRequest request, string campo)
    {
        if (!request.HasFormContentType || !request.Form.TryGetValue(campo, out var valor) || string.IsNullOrWhiteSpace(valor))
            return null;
        return long.TryParse(valor, out var resultado) ? resultado : throw RegraNegocioException.Invalida($"Valor inválido para {campo}.");
    }
}
