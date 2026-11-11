using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using VeAI.Api.Erros;
using VeAI.Domain.Enums;
using VeAI.Infrastructure.Seguranca;

namespace VeAI.Api.Seguranca;

public static class AutenticacaoJwt
{
    public const string PoliticaGestor = "Gestor";
    public const string PoliticaCidadao = "Cidadao";

    /// <summary>
    /// JWT Bearer (HS256) com os perfis CIDADAO e GESTOR. Toda rota exige login, salvo as marcadas como anônimas.
    /// Sem Jwt__Segredo configurado, gera uma chave aleatória a cada inicialização (a API sobe, mas os tokens
    /// deixam de valer ao reiniciar): nenhum segredo fica no código ou no repositório.
    /// </summary>
    public static IServiceCollection AddAutenticacaoJwt(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.Secao).Get<JwtOptions>() ?? new JwtOptions();
        if (string.IsNullOrWhiteSpace(jwt.Segredo) || jwt.Segredo.Length < 32)
        {
            jwt.Segredo = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
            ChaveTemporaria = true;
        }

        services.Configure<JwtOptions>(o =>
        {
            o.Segredo = jwt.Segredo;
            o.Emissor = jwt.Emissor;
            o.ValidadeHoras = jwt.ValidadeHoras;
        });

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false; // mantém "sub" e "papel" com os nomes do token
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Emissor,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = jwt.Chave(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = JwtOptions.ClaimPapel
                };
                o.Events = new JwtBearerEvents
                {
                    // 401 e 403 no mesmo formato de erro do resto da API.
                    OnChallenge = async contexto =>
                    {
                        contexto.HandleResponse();
                        var mensagem = contexto.AuthenticateFailure is SecurityTokenExpiredException
                            ? "Sua sessão expirou. Faça login de novo."
                            : "Faça login para continuar.";
                        await ErroResposta.EscreverAsync(contexto.HttpContext, StatusCodes.Status401Unauthorized, mensagem);
                    },
                    OnForbidden = contexto => ErroResposta.EscreverAsync(contexto.HttpContext,
                        StatusCodes.Status403Forbidden, "Você não tem permissão para esta ação.")
                };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser().Build())
            .AddPolicy(PoliticaGestor, p => p.RequireRole(nameof(Papel.GESTOR)))
            .AddPolicy(PoliticaCidadao, p => p.RequireRole(nameof(Papel.CIDADAO)));

        return services;
    }

    /// <summary>true quando a API subiu sem Jwt__Segredo e está usando uma chave gerada na hora.</summary>
    public static bool ChaveTemporaria { get; private set; }

    /// <summary>Id do usuário logado (claim "sub").</summary>
    public static long UsuarioId(this ClaimsPrincipal usuario) =>
        long.Parse(usuario.FindFirstValue(JwtRegisteredClaimNames.Sub)
                   ?? throw new InvalidOperationException("Token sem a claim sub."));
}
