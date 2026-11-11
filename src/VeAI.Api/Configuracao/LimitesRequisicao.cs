using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using VeAI.Api.Erros;

namespace VeAI.Api.Configuracao;

/// <summary>Limites configuráveis (seção "RateLimit", ex.: RateLimit__FotosPorMinuto=20 no .env).</summary>
public sealed class LimitesOptions
{
    public const string Secao = "RateLimit";

    /// <summary>Fotos enviadas à IA por usuário por minuto (descrição + pergunta livre). Controla o custo da OpenAI.</summary>
    public int FotosPorMinuto { get; set; } = 20;
    /// <summary>Tentativas de login/cadastro por IP por minuto (contra força bruta).</summary>
    public int LoginPorMinuto { get; set; } = 10;
    /// <summary>Limite geral por usuário (ou IP, se anônimo) para qualquer rota.</summary>
    public int RequisicoesPorMinuto { get; set; } = 300;
}

/// <summary>
/// Rate limiting com o middleware nativo do ASP.NET Core (Microsoft.AspNetCore.RateLimiting):
/// - "fotos": janela fixa de 1 minuto por usuário nas rotas que chamam a IA;
/// - "login": janela deslizante de 1 minuto por IP em /auth;
/// - global: token bucket por usuário/IP em todas as rotas.
/// Excesso → 429 com o header Retry-After e a mensagem no formato de erro da API.
/// </summary>
public static class LimitesRequisicao
{
    public const string PoliticaFotos = "fotos";
    public const string PoliticaLogin = "login";

    public static IServiceCollection AddLimitesRequisicao(this IServiceCollection services, IConfiguration configuration)
    {
        var limites = configuration.GetSection(LimitesOptions.Secao).Get<LimitesOptions>() ?? new LimitesOptions();

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            o.AddPolicy(PoliticaFotos, http => RateLimitPartition.GetFixedWindowLimiter(Chave(http), _ =>
                new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limites.FotosPorMinuto,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

            o.AddPolicy(PoliticaLogin, http => RateLimitPartition.GetSlidingWindowLimiter(Ip(http), _ =>
                new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = limites.LoginPorMinuto,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 6,
                    QueueLimit = 0
                }));

            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
                RateLimitPartition.GetTokenBucketLimiter(Chave(http), _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = limites.RequisicoesPorMinuto,
                    TokensPerPeriod = limites.RequisicoesPorMinuto,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

            o.OnRejected = async (contexto, ct) =>
            {
                var segundos = contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera)
                    ? Math.Max(1, (int)Math.Ceiling(espera.TotalSeconds))
                    : 60;
                var http = contexto.HttpContext;
                http.Response.Headers.RetryAfter = segundos.ToString(CultureInfo.InvariantCulture);

                var politica = http.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
                var mensagem = politica switch
                {
                    PoliticaFotos => $"Muitas fotos em pouco tempo. Aguarde {segundos} segundos e tente de novo.",
                    PoliticaLogin => $"Muitas tentativas em pouco tempo. Aguarde {segundos} segundos e tente de novo.",
                    _ => $"Muitas requisições em pouco tempo. Aguarde {segundos} segundos e tente de novo."
                };
                await ErroResposta.EscreverAsync(http, StatusCodes.Status429TooManyRequests, mensagem);
            };
        });
        return services;
    }

    /// <summary>Usuário logado (claim sub); sem login, o IP.</summary>
    private static string Chave(HttpContext http) =>
        http.User.FindFirstValue(JwtRegisteredClaimNames.Sub) is { } id ? "u:" + id : Ip(http);

    private static string Ip(HttpContext http) => "ip:" + (http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido");
}
