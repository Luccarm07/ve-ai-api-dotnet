using System.Security.Claims;
using VeAI.Api.Erros;
using VeAI.Api.Seguranca;
using VeAI.Application.Dtos;
using VeAI.Application.Servicos;

namespace VeAI.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/auth").WithTags("Autenticação").AllowAnonymous()
            .RequireRateLimiting(Configuracao.LimitesRequisicao.PoliticaLogin); // contra força bruta: 10/min por IP

        grupo.MapPost("/registrar", async (RegistroRequest req, AuthServico servico, CancellationToken ct) =>
                Results.Created("/usuarios/me", await servico.RegistrarAsync(req, ct)))
            .WithName("Registrar")
            .WithSummary("Cria uma conta de cidadão")
            .WithDescription("Gestores não se cadastram: vêm do seed. O e-mail é único.")
            .ComValidacao<RegistroRequest>()
            .Produces<UsuarioResposta>(StatusCodes.Status201Created)
            .ProducesErro(StatusCodes.Status409Conflict, StatusCodes.Status429TooManyRequests);

        grupo.MapPost("/login", async (LoginRequest req, AuthServico servico, CancellationToken ct) =>
                Results.Ok(await servico.LoginAsync(req, ct)))
            .WithName("Login")
            .WithSummary("Entra com e-mail e senha e recebe o JWT (válido por 8 h)")
            .WithDescription("Limite de 10 tentativas por minuto por IP (contra força bruta): acima disso, 429 com Retry-After.")
            .ComValidacao<LoginRequest>()
            .Produces<TokenResposta>()
            .ProducesErro(StatusCodes.Status401Unauthorized, StatusCodes.Status429TooManyRequests);

        return app;
    }
}

public static class UsuarioEndpoints
{
    public static IEndpointRouteBuilder MapUsuarios(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/usuarios/me").WithTags("Usuário");

        grupo.MapGet("", async (ClaimsPrincipal user, UsuarioServico servico, CancellationToken ct) =>
                Results.Ok(await servico.BuscarAsync(user.UsuarioId(), ct)))
            .WithName("MeusDados")
            .WithSummary("Dados do usuário logado (gestor inclui as regiões atendidas)")
            .Produces<UsuarioResposta>()
            .ProducesErro(StatusCodes.Status404NotFound);

        grupo.MapPut("/preferencias", async (PreferenciasRequest req, ClaimsPrincipal user, UsuarioServico servico,
                    CancellationToken ct) =>
                Results.Ok(await servico.AtualizarPreferenciasAsync(user.UsuarioId(), req, ct)))
            .WithName("AtualizarPreferencias")
            .WithSummary("Atualiza velocidade da voz (0,5 a 2,0) e pergunta livre")
            .ComValidacao<PreferenciasRequest>()
            .Produces<UsuarioResposta>();

        return app;
    }
}

public static class RegiaoEndpoints
{
    public static IEndpointRouteBuilder MapRegioes(this IEndpointRouteBuilder app)
    {
        app.MapGet("/regioes", async (RegiaoServico servico, CancellationToken ct) => Results.Ok(await servico.ListarAsync(ct)))
            .WithTags("Regiões")
            .WithName("ListarRegioes")
            .WithSummary("Lista as regiões atendidas pela rede de apoio")
            .Produces<IReadOnlyList<RegiaoResposta>>();
        return app;
    }
}
