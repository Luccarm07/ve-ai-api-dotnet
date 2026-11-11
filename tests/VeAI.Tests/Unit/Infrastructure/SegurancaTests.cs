using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using VeAI.Domain.Entities;
using VeAI.Infrastructure.Seguranca;
using VeAI.Tests.Integration;

namespace VeAI.Tests.Unit.Infrastructure;

public class SegurancaTests
{
    [Fact(DisplayName = "BCrypt confere a senha Veai@2026 contra o hash do seed Oracle (compatível com a API Java)")]
    public void HashDoSeed()
    {
        var hasher = new SenhaHasherBCrypt();

        Assert.True(hasher.Confere("Veai@2026", DadosTeste.SenhaHash));
        Assert.False(hasher.Confere("veai@2026", DadosTeste.SenhaHash));
        Assert.False(hasher.Confere("Veai@2026", "hash-invalido"));
    }

    [Fact(DisplayName = "BCrypt gera hash novo que confere e nunca guarda a senha em texto")]
    public void HashNovo()
    {
        var hasher = new SenhaHasherBCrypt();

        var hash = hasher.Gerar("Senha@Forte1");

        Assert.StartsWith("$2", hash);
        Assert.DoesNotContain("Senha@Forte1", hash);
        Assert.True(hasher.Confere("Senha@Forte1", hash));
    }

    [Fact(DisplayName = "JWT: sub, papel, emissor e validade de 8 h")]
    public void Token()
    {
        var relogio = new FakeTimeProvider(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));
        var opcoes = Options.Create(new JwtOptions { Segredo = ApiFactory.SegredoJwt });
        var gestor = Usuario.NovoGestor("Marina Alves", DadosTeste.EmailMarina, "hash", [], relogio.GetUtcNow().UtcDateTime);

        var emitido = new GeradorTokenJwt(opcoes, relogio).Emitir(gestor);

        var jwt = new JsonWebToken(emitido.Token);
        Assert.Equal("GESTOR", jwt.GetClaim("papel").Value);
        Assert.Equal("Marina Alves", jwt.GetClaim("nome").Value);
        Assert.Equal("ve-ai-api", jwt.Issuer);
        Assert.Equal(new DateTime(2026, 11, 10, 20, 0, 0, DateTimeKind.Utc), emitido.ExpiraEm);
        Assert.Equal("HS256", jwt.Alg);
    }
}
