using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace VeAI.Tests.Integration;

/// <summary>API com no máximo 3 fotos por minuto por usuário, para testar o 429.</summary>
public sealed class LimiteFotosFactory : ApiFactory
{
    protected override int FotosPorMinuto => 3;
}

/// <summary>API com no máximo 3 tentativas de login por minuto por IP.</summary>
public sealed class LimiteLoginFactory : ApiFactory
{
    protected override int LoginPorMinuto => 3;
}

public class RateLimitFotosTests(LimiteFotosFactory factory) : IClassFixture<LimiteFotosFactory>
{
    [Fact(DisplayName = "429: a 4ª foto no minuto é recusada com Retry-After; outro usuário tem a própria cota; pergunta conta junto")]
    public async Task Fotos429()
    {
        var ana = await factory.ClienteLogadoAsync(DadosTeste.EmailAna);
        var bruno = await factory.ClienteLogadoAsync(DadosTeste.EmailBruno);

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.Created, (await ana.PostAsync("/descricoes", Integration.Fotos.Formulario(Integration.Fotos.Jpeg(1)))).StatusCode);
        var excesso = await ana.PostAsync("/descricoes", Integration.Fotos.Formulario(Integration.Fotos.Jpeg(1)));

        Assert.Equal(HttpStatusCode.TooManyRequests, excesso.StatusCode);
        var segundos = int.Parse(excesso.Headers.GetValues("Retry-After").Single());
        Assert.InRange(segundos, 1, 60);
        var erro = await excesso.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(429, erro.GetProperty("status").GetInt32());
        Assert.Equal($"Muitas fotos em pouco tempo. Aguarde {segundos} segundos e tente de novo.", erro.GetProperty("mensagem").GetString());

        // A pergunta livre conta no mesmo limite.
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await ana.PostAsync("/descricoes/1/pergunta?pergunta=oi", Integration.Fotos.Formulario(Integration.Fotos.Jpeg(1)))).StatusCode);

        // Outras rotas e outros usuários não são afetados.
        Assert.Equal(HttpStatusCode.OK, (await ana.GetAsync("/descricoes")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await bruno.PostAsync("/descricoes", Integration.Fotos.Formulario(Integration.Fotos.Jpeg(1)))).StatusCode);
    }
}

public class RateLimitLoginTests(LimiteLoginFactory factory) : IClassFixture<LimiteLoginFactory>
{
    [Fact(DisplayName = "429: excesso de tentativas de login no minuto (contra força bruta)")]
    public async Task Login429()
    {
        var cliente = factory.CreateClient();
        var corpo = new { email = DadosTeste.EmailAna, senha = "errada-123" };

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.PostAsJsonAsync("/auth/login", corpo)).StatusCode);
        var excesso = await cliente.PostAsJsonAsync("/auth/login", corpo);

        Assert.Equal(HttpStatusCode.TooManyRequests, excesso.StatusCode);
        Assert.True(excesso.Headers.Contains("Retry-After"));
        Assert.StartsWith("Muitas tentativas em pouco tempo.", (await excesso.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("mensagem").GetString());
    }
}
