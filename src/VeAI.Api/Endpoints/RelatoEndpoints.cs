using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using VeAI.Api.Erros;
using VeAI.Api.Seguranca;
using VeAI.Application.Dtos;
using VeAI.Application.Servicos;
using VeAI.Domain.Enums;

namespace VeAI.Api.Endpoints;

public static class RelatoEndpoints
{
    public static IEndpointRouteBuilder MapRelatos(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/relatos")
            .WithTags("Relatos")
            .RequireAuthorization(AutenticacaoJwt.PoliticaCidadao)
            .ProducesErroGrupo(StatusCodes.Status403Forbidden);

        grupo.MapPost("", async (
                RelatoRequest req,
                [FromHeader(Name = IdempotenciaServico.Header)] string? idempotencyKey,
                ClaimsPrincipal user, RelatoServico relatos, IdempotenciaServico idempotencia,
                HttpContext http, CancellationToken ct) =>
            {
                var usuarioId = user.UsuarioId();
                var resposta = await idempotencia.ExecutarAsync(usuarioId, idempotencyKey, req, async () =>
                {
                    var corpo = await relatos.RegistrarAsync(usuarioId, req, ct);
                    return (corpo.StatusHttp(), corpo);
                }, ct);

                if (resposta.Repetida)
                    http.Response.Headers[IdempotenciaServico.HeaderRepetida] = "true";
                if (resposta.Status == StatusCodes.Status201Created)
                    http.Response.Headers.Location = $"/relatos/{resposta.Corpo.Relato.Id}";
                return Results.Json(resposta.Corpo, statusCode: resposta.Status);
            })
            .WithName("RegistrarRelato")
            .WithSummary("Registra um relato ou +1 apoio (regra dos 30 m), com Idempotency-Key")
            .WithDescription(
                "Se já existe um relato não finalizado do mesmo tipo a até 30 m, registra um apoio nele (200, APOIO_REGISTRADO); " +
                "se você é o autor ou já apoiou, devolve 200 JA_REGISTRADO. Caso contrário cria o relato (201, CRIADO).\n\n" +
                "**Idempotency-Key** (opcional, recomendado: um UUID por envio): repetir a mesma chave com o mesmo corpo em até 24 h " +
                "devolve a mesma resposta, com o header `Idempotent-Replayed: true`, sem criar outro relato nem outro apoio. " +
                "A mesma chave com outro corpo devolve 422.")
            .ComValidacao<RelatoRequest>()
            .Produces<RegistroRelatoResposta>(StatusCodes.Status201Created)
            .Produces<RegistroRelatoResposta>(StatusCodes.Status200OK)
            .ProducesErro(StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity);

        grupo.MapGet("/meus", async (int? page, int? size, ClaimsPrincipal user, RelatoServico relatos, CancellationToken ct) =>
                Results.Ok(await relatos.MeusAsync(user.UsuarioId(), page, size, ct)))
            .WithName("MeusRelatos")
            .WithSummary("Relatos criados por mim, do mais recente para o mais antigo (page a partir de 0, size até 50)")
            .Produces<PaginaResposta<RelatoResposta>>();

        grupo.MapGet("/{id:long}", async (long id, ClaimsPrincipal user, RelatoServico relatos, CancellationToken ct) =>
                Results.Ok(await relatos.BuscarAsync(user.UsuarioId(), id, ct)))
            .WithName("BuscarRelato")
            .WithSummary("Detalhe do relato (para o autor e para quem apoiou)")
            .Produces<RelatoResposta>()
            .ProducesErro(StatusCodes.Status404NotFound);

        grupo.MapPut("/{id:long}", async (long id, RelatoEdicaoRequest req, ClaimsPrincipal user, RelatoServico relatos,
                    CancellationToken ct) =>
                Results.Ok(await relatos.EditarAsync(user.UsuarioId(), id, req, ct)))
            .WithName("EditarRelato")
            .WithSummary("Edita tipo, gravidade, texto e endereço (só enquanto ABERTO; a localização não muda)")
            .ComValidacao<RelatoEdicaoRequest>()
            .Produces<RelatoResposta>()
            .ProducesErro(StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity);

        grupo.MapDelete("/{id:long}", async (long id, ClaimsPrincipal user, RelatoServico relatos, CancellationToken ct) =>
            {
                await relatos.ExcluirAsync(user.UsuarioId(), id, ct);
                return Results.NoContent();
            })
            .WithName("ExcluirRelato")
            .WithSummary("Exclui o relato (só enquanto ABERTO e sem apoios de outras pessoas)")
            .WithDescription("409 quando outras pessoas já confirmaram o relato; 422 quando ele já saiu de ABERTO.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesErro(StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status422UnprocessableEntity);

        return app;
    }

    public static IEndpointRouteBuilder MapGestor(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/gestor/relatos")
            .WithTags("Gestor")
            .RequireAuthorization(AutenticacaoJwt.PoliticaGestor)
            .ProducesErroGrupo(StatusCodes.Status403Forbidden);

        grupo.MapGet("", async (StatusRelato? status, long? regiaoId, int? page, int? size, ClaimsPrincipal user,
                    GestorRelatoServico gestor, CancellationToken ct) =>
                Results.Ok(await gestor.FilaAsync(user.UsuarioId(), status, regiaoId, page, size, ct)))
            .WithName("FilaGestor")
            .WithSummary("Fila de relatos das minhas regiões, por prioridade (maior primeiro)")
            .WithDescription("Filtros opcionais: status e regiaoId (403 se for uma região que você não atende).")
            .Produces<PaginaResposta<RelatoResposta>>();

        grupo.MapGet("/{id:long}", async (long id, ClaimsPrincipal user, GestorRelatoServico gestor, CancellationToken ct) =>
                Results.Ok(await gestor.BuscarAsync(user.UsuarioId(), id, ct)))
            .WithName("BuscarRelatoGestor")
            .WithSummary("Detalhe de um relato da minha região")
            .Produces<RelatoResposta>()
            .ProducesErro(StatusCodes.Status404NotFound);

        grupo.MapPatch("/{id:long}/status", async (long id, StatusRequest req, ClaimsPrincipal user, GestorRelatoServico gestor,
                    CancellationToken ct) =>
                Results.Ok(await gestor.AtualizarStatusAsync(user.UsuarioId(), id, req, ct)))
            .WithName("AtualizarStatus")
            .WithSummary("Muda o status: ABERTO → EM_TRIAGEM → ENCAMINHADO (protocolo) → RESOLVIDO, ou DESCARTADO (motivo)")
            .WithDescription("422 para transição inválida (pular etapa, voltar, sair de status final) ou sem protocolo/motivo.")
            .ComValidacao<StatusRequest>()
            .Produces<RelatoResposta>()
            .ProducesErro(StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity);

        grupo.MapGet("/{id:long}/historico", async (long id, ClaimsPrincipal user, GestorRelatoServico gestor, CancellationToken ct) =>
                Results.Ok(await gestor.HistoricoAsync(user.UsuarioId(), id, ct)))
            .WithName("HistoricoRelato")
            .WithSummary("Histórico de mudanças de status do relato")
            .Produces<IReadOnlyList<AtualizacaoResposta>>()
            .ProducesErro(StatusCodes.Status404NotFound);

        return app;
    }
}
