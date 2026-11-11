using System.Net;
using System.Text.Json;

namespace VeAI.Tests.Integration;

public class SmokeTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact(DisplayName = "GET /health: API e banco saudáveis, com o detalhe de cada verificação")]
    public async Task Health()
    {
        var resposta = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", json.RootElement.GetProperty("status").GetString());
        var nomes = json.RootElement.GetProperty("verificacoes").EnumerateArray().Select(v => v.GetProperty("nome").GetString());
        Assert.Contains("api", nomes);
        Assert.Contains("oracle", nomes);
    }

    [Fact(DisplayName = "GET /health/live responde só com o processo")]
    public async Task Live()
    {
        var resposta = await factory.CreateClient().GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.DoesNotContain("oracle", await resposta.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "documento OpenAPI publicado")]
    public async Task OpenApi()
    {
        var resposta = await factory.CreateClient().GetAsync("/openapi/v1.json");

        resposta.EnsureSuccessStatusCode();
        Assert.Contains("Vê AI", await resposta.Content.ReadAsStringAsync());
    }
}
