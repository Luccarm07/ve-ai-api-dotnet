using Microsoft.AspNetCore.Mvc.Testing;

namespace VeAI.Tests.Integration;

public class SmokeTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_responde_200()
    {
        var resposta = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(System.Net.HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("Healthy", await resposta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Documento_OpenAPI_esta_publicado()
    {
        var resposta = await factory.CreateClient().GetAsync("/openapi/v1.json");

        resposta.EnsureSuccessStatusCode();
        Assert.Contains("Vê AI", await resposta.Content.ReadAsStringAsync());
    }
}
