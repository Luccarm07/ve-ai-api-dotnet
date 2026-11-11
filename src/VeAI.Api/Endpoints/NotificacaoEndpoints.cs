using System.Security.Claims;
using VeAI.Api.Erros;
using VeAI.Api.Seguranca;
using VeAI.Application.Dtos;
using VeAI.Application.Servicos;

namespace VeAI.Api.Endpoints;

public static class NotificacaoEndpoints
{
    public static IEndpointRouteBuilder MapNotificacoes(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/notificacoes").WithTags("Notificações");

        grupo.MapGet("", async (bool? lida, int? page, int? size, ClaimsPrincipal user, NotificacaoServico servico, CancellationToken ct) =>
                Results.Ok(await servico.ListarAsync(user.UsuarioId(), lida, page, size, ct)))
            .WithName("MinhasNotificacoes")
            .WithSummary("Minhas notificações, da mais recente para a mais antiga (lida=false: só as não lidas)")
            .WithDescription("Geradas pelo consumidor da fila veai.notificacoes (RabbitMQ) e lidas em voz alta pelo app. " +
                             "Gestores recebem os relatos novos da região; cidadãos, os apoios e as mudanças de status.")
            .Produces<PaginaResposta<NotificacaoResposta>>();

        grupo.MapPatch("/{id:long}/lida", async (long id, ClaimsPrincipal user, NotificacaoServico servico, CancellationToken ct) =>
                Results.Ok(await servico.MarcarComoLidaAsync(user.UsuarioId(), id, ct)))
            .WithName("MarcarNotificacaoLida")
            .WithSummary("Marca a notificação como lida")
            .Produces<NotificacaoResposta>()
            .ProducesErro(StatusCodes.Status404NotFound);

        return app;
    }
}
