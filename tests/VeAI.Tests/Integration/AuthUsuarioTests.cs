using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;

namespace VeAI.Tests.Integration;

public class AuthUsuarioTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<JsonElement> AssertErro(HttpResponseMessage r, HttpStatusCode status, string mensagem)
    {
        Assert.Equal(status, r.StatusCode);
        var json = await Json(r);
        Assert.Equal((int)status, json.GetProperty("status").GetInt32());
        Assert.Equal(mensagem, json.GetProperty("mensagem").GetString());
        Assert.True(json.TryGetProperty("timestamp", out _));
        Assert.True(json.TryGetProperty("erro", out _));
        Assert.Equal(JsonValueKind.Array, json.GetProperty("campos").ValueKind);
        return json;
    }

    [Fact(DisplayName = "POST /auth/login: cidadã do seed recebe JWT com sub e papel")]
    public async Task LoginCidada()
    {
        var r = await factory.CreateClient().PostAsJsonAsync("/auth/login", new { email = "ANA.souza@veai.dev", senha = DadosTeste.Senha });

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var json = await Json(r);
        Assert.Equal("Bearer", json.GetProperty("tipo").GetString());
        Assert.Equal("CIDADAO", json.GetProperty("papel").GetString());
        Assert.Equal(DadosTeste.EmailAna, json.GetProperty("usuario").GetProperty("email").GetString());
        Assert.True(json.GetProperty("expiraEm").GetDateTime() > DateTime.UtcNow.AddHours(7));

        var jwt = new JsonWebToken(json.GetProperty("token").GetString());
        Assert.Equal("CIDADAO", jwt.GetClaim("papel").Value);
        Assert.Equal(json.GetProperty("usuario").GetProperty("id").GetInt64().ToString(), jwt.Subject);
        Assert.Equal("ve-ai-api", jwt.Issuer);
    }

    [Fact(DisplayName = "POST /auth/login: gestora recebe as regiões que atende, em ordem")]
    public async Task LoginGestora()
    {
        var r = await factory.CreateClient().PostAsJsonAsync("/auth/login", new { email = DadosTeste.EmailMarina, senha = DadosTeste.Senha });

        var json = await Json(r);
        Assert.Equal("GESTOR", json.GetProperty("papel").GetString());
        var regioes = json.GetProperty("usuario").GetProperty("regioes").EnumerateArray().Select(x => x.GetProperty("nome").GetString());
        Assert.Equal(["Pinheiros", "Sé"], regioes);
    }

    [Theory(DisplayName = "POST /auth/login: senha errada ou e-mail inexistente → 401 com a mesma mensagem")]
    [InlineData(DadosTeste.EmailAna, "SenhaErrada1")]
    [InlineData("ninguem@veai.dev", "Veai@2026")]
    public async Task LoginInvalido(string email, string senha)
    {
        var r = await factory.CreateClient().PostAsJsonAsync("/auth/login", new { email, senha });

        await AssertErro(r, HttpStatusCode.Unauthorized, "E-mail ou senha incorretos.");
    }

    [Fact(DisplayName = "POST /auth/login: campos inválidos → 400 com a lista de campos")]
    public async Task LoginValidacao()
    {
        var r = await factory.CreateClient().PostAsJsonAsync("/auth/login", new { email = "nao-e-email", senha = "" });

        var erro = await AssertErro(r, HttpStatusCode.BadRequest, "Confira os dados enviados.");
        var campos = erro.GetProperty("campos").EnumerateArray()
            .Select(c => (c.GetProperty("campo").GetString(), c.GetProperty("mensagem").GetString())).ToList();
        Assert.Contains(("email", "E-mail inválido."), campos);
        Assert.Contains(("senha", "Informe sua senha."), campos);
    }

    [Fact(DisplayName = "JSON malformado → 400 em português")]
    public async Task JsonMalformado()
    {
        var r = await factory.CreateClient().PostAsync("/auth/login", new StringContent("{email:", Encoding.UTF8, "application/json"));

        await AssertErro(r, HttpStatusCode.BadRequest, "Não entendi os dados enviados. Confira o formato e os valores.");
    }

    [Fact(DisplayName = "POST /auth/registrar: cria cidadão (201), permite login e recusa e-mail repetido (409)")]
    public async Task Registrar()
    {
        var cliente = factory.CreateClient();
        var corpo = new { nome = "Paula Nova", email = "paula.nova@veai.dev", senha = "Senha@Forte1" };

        var criado = await cliente.PostAsJsonAsync("/auth/registrar", corpo);
        Assert.Equal(HttpStatusCode.Created, criado.StatusCode);
        Assert.Equal("/usuarios/me", criado.Headers.Location?.ToString());
        var json = await Json(criado);
        Assert.Equal("CIDADAO", json.GetProperty("papel").GetString());
        Assert.False(json.TryGetProperty("senhaHash", out _));
        Assert.Empty(json.GetProperty("regioes").EnumerateArray());

        var login = await cliente.PostAsJsonAsync("/auth/login", new { email = corpo.email, senha = corpo.senha });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var repetido = await cliente.PostAsJsonAsync("/auth/registrar", corpo with { email = "PAULA.NOVA@veai.dev" });
        await AssertErro(repetido, HttpStatusCode.Conflict, "Já existe uma conta com este e-mail.");
    }

    [Fact(DisplayName = "POST /auth/registrar: senha curta → 400")]
    public async Task RegistrarSenhaCurta()
    {
        var r = await factory.CreateClient().PostAsJsonAsync("/auth/registrar", new { nome = "X", email = "x@veai.dev", senha = "123" });

        var erro = await AssertErro(r, HttpStatusCode.BadRequest, "Confira os dados enviados.");
        Assert.Contains("entre 8 e 72", erro.GetRawText());
    }

    [Fact(DisplayName = "rota protegida sem token → 401; token adulterado → 401")]
    public async Task SemToken()
    {
        var cliente = factory.CreateClient();
        await AssertErro(await cliente.GetAsync("/usuarios/me"), HttpStatusCode.Unauthorized, "Faça login para continuar.");

        var logado = await factory.ClienteLogadoAsync(DadosTeste.EmailAna);
        var token = logado.DefaultRequestHeaders.Authorization!.Parameter!;
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token[..^4] + "AAAA");
        await AssertErro(await cliente.GetAsync("/usuarios/me"), HttpStatusCode.Unauthorized, "Faça login para continuar.");
    }

    [Fact(DisplayName = "GET /usuarios/me e PUT /usuarios/me/preferencias")]
    public async Task MeEPreferencias()
    {
        var cliente = await factory.ClienteLogadoAsync(DadosTeste.EmailBruno);

        var me = await Json(await cliente.GetAsync("/usuarios/me"));
        Assert.Equal("Bruno Lima", me.GetProperty("nome").GetString());

        var ok = await cliente.PutAsJsonAsync("/usuarios/me/preferencias", new { velocidadeVoz = 1.5, perguntaLivreAtiva = true });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var depois = await Json(await cliente.GetAsync("/usuarios/me"));
        Assert.Equal(1.5, depois.GetProperty("velocidadeVoz").GetDouble());
        Assert.True(depois.GetProperty("perguntaLivreAtiva").GetBoolean());

        var invalido = await cliente.PutAsJsonAsync("/usuarios/me/preferencias", new { velocidadeVoz = 2.5 });
        var erro = await AssertErro(invalido, HttpStatusCode.BadRequest, "Confira os dados enviados.");
        var campos = erro.GetProperty("campos").EnumerateArray().Select(c => c.GetProperty("campo").GetString()).ToList();
        Assert.Contains("velocidadeVoz", campos);
        Assert.Contains("perguntaLivreAtiva", campos);
    }

    [Fact(DisplayName = "GET /regioes: lista as 3 regiões com o centro")]
    public async Task Regioes()
    {
        var cliente = await factory.ClienteLogadoAsync(DadosTeste.EmailAna);

        var regioes = (await Json(await cliente.GetAsync("/regioes"))).EnumerateArray().ToList();

        Assert.Equal(3, regioes.Count);
        Assert.All(regioes, r => Assert.Equal("SP", r.GetProperty("uf").GetString()));
        Assert.All(regioes, r => Assert.NotEqual(JsonValueKind.Null, r.GetProperty("latitude").ValueKind));
    }

    [Fact(DisplayName = "rota inexistente (logado) → 404 no formato de erro")]
    public async Task RotaInexistente()
    {
        var cliente = await factory.ClienteLogadoAsync(DadosTeste.EmailAna);

        await AssertErro(await cliente.GetAsync("/nao-existe"), HttpStatusCode.NotFound, "Endereço não encontrado.");
    }

    [Fact(DisplayName = "OpenAPI: esquema Bearer publicado e rotas protegidas marcadas")]
    public async Task OpenApiSeguranca()
    {
        var doc = await Json(await factory.CreateClient().GetAsync("/openapi/v1.json"));

        Assert.Equal("bearer", doc.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString());
        Assert.True(doc.GetProperty("paths").GetProperty("/usuarios/me").GetProperty("get").TryGetProperty("security", out _));
        Assert.False(doc.GetProperty("paths").GetProperty("/auth/login").GetProperty("post").TryGetProperty("security", out _));
    }
}
