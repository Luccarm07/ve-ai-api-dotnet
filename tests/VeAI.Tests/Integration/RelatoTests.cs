using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using VeAI.Domain.Eventos;

namespace VeAI.Tests.Integration;

public class RelatoTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // Cada teste usa um ponto diferente perto de Pinheiros (0,001° ≈ 111 m), para a regra dos 30 m não misturar testes.
    private const double LatBase = -23.5660, LonBase = -46.6845;
    private const double Metro = 1 / 111_320d;

    private static object Corpo(double lat, double lon = LonBase, string tipo = "CALCADA_DANIFICADA", string texto = "Buraco grande") =>
        new { tipo, gravidade = "ALTA", texto, latitude = lat, longitude = lon, endereco = "Rua dos Pinheiros, 500" };

    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<(HttpStatusCode Status, JsonElement Json)> Enviar(HttpClient c, object corpo, string? chave = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/relatos") { Content = JsonContent.Create(corpo) };
        if (chave is not null)
            req.Headers.Add("Idempotency-Key", chave);
        var r = await c.SendAsync(req);
        return (r.StatusCode, await Json(r));
    }

    [Fact(DisplayName = "POST /relatos: cria (201 CRIADO) na região mais próxima e publica relato.criado")]
    public async Task Criar()
    {
        var ana = await factory.ClienteLogadoAsync(DadosTeste.EmailAna);

        var r = await ana.PostAsJsonAsync("/relatos", Corpo(LatBase + 0.001));

        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var json = await Json(r);
        Assert.Equal("CRIADO", json.GetProperty("resultado").GetString());
        Assert.False(json.GetProperty("apoioRegistrado").GetBoolean());
        var relato = json.GetProperty("relato");
        Assert.Equal("Pinheiros", relato.GetProperty("regiao").GetProperty("nome").GetString());
        Assert.Equal("ABERTO", relato.GetProperty("status").GetString());
        Assert.Equal(5, relato.GetProperty("prioridade").GetInt32()); // ALTA × (1 + 0)
        Assert.Equal($"/relatos/{relato.GetProperty("id").GetInt64()}", r.Headers.Location?.ToString());
        Assert.Contains(factory.Eventos.Todos, e => e.Tipo == TipoEvento.CRIADO && e.RelatoId == relato.GetProperty("id").GetInt64());
    }

    [Fact(DisplayName = "regra dos 30 m: outro cidadão a ~10 m vira apoio (200); repetir ou ser o autor dá JA_REGISTRADO")]
    public async Task RegraDos30Metros()
    {
        var ana = await factory.ClienteLogadoAsync(DadosTeste.EmailAna);
        var bruno = await factory.ClienteLogadoAsync(DadosTeste.EmailBruno);
        var lat = LatBase + 0.002;

        var (_, criado) = await Enviar(ana, Corpo(lat));
        var id = criado.GetProperty("relato").GetProperty("id").GetInt64();

        var (status, apoio) = await Enviar(bruno, Corpo(lat + 10 * Metro, texto: "Mesmo buraco"));
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("APOIO_REGISTRADO", apoio.GetProperty("resultado").GetString());
        Assert.True(apoio.GetProperty("apoioRegistrado").GetBoolean());
        Assert.Equal(id, apoio.GetProperty("relato").GetProperty("id").GetInt64());
        Assert.Equal(1, apoio.GetProperty("relato").GetProperty("totalApoios").GetInt32());
        Assert.Equal(10, apoio.GetProperty("relato").GetProperty("prioridade").GetInt32()); // 5 × (1 + 1)
        Assert.Contains(factory.Eventos.Todos, e => e.Tipo == TipoEvento.APOIADO && e.RelatoId == id);

        var (s2, deNovo) = await Enviar(bruno, Corpo(lat));
        Assert.Equal(HttpStatusCode.OK, s2);
        Assert.Equal("JA_REGISTRADO", deNovo.GetProperty("resultado").GetString());
        Assert.Equal(1, deNovo.GetProperty("relato").GetProperty("totalApoios").GetInt32());

        var (_, autor) = await Enviar(ana, Corpo(lat));
        Assert.Equal("JA_REGISTRADO", autor.GetProperty("resultado").GetString());

        // O apoiador também pode acompanhar o relato.
        Assert.Equal(HttpStatusCode.OK, (await bruno.GetAsync($"/relatos/{id}")).StatusCode);
    }

    [Fact(DisplayName = "regra dos 30 m: tipo diferente no mesmo ponto ou a ~35 m cria relato novo")]
    public async Task ForaDaRegra()
    {
        var ana = await factory.ClienteLogadoAsync(DadosTeste.EmailAna);
        var bruno = await factory.ClienteLogadoAsync(DadosTeste.EmailBruno);
        var lat = LatBase + 0.003;
        await Enviar(ana, Corpo(lat));

        var (s1, outroTipo) = await Enviar(bruno, Corpo(lat, tipo: "RAMPA"));
        var (s2, longe) = await Enviar(bruno, Corpo(lat + 35 * Metro));

        Assert.Equal(HttpStatusCode.Created, s1);
        Assert.Equal("CRIADO", outroTipo.GetProperty("resultado").GetString());
        Assert.Equal(HttpStatusCode.Created, s2);
        Assert.Equal("CRIADO", longe.GetProperty("resultado").GetString());
    }

    [Fact(DisplayName = "Idempotency-Key: repetir devolve a mesma resposta sem criar outro relato")]
    public async Task Idempotencia()
    {
        var ana = await factory.ClienteLogadoAsync(DadosTeste.EmailAna);
        var corpo = Corpo(LatBase + 0.004);
        var chave = Guid.NewGuid().ToString();
        var antes = (await Json(await ana.GetAsync("/relatos/meus?size=50"))).GetProperty("totalElementos").GetInt64();

        var req1 = new HttpRequestMessage(HttpMethod.Post, "/relatos") { Content = JsonContent.Create(corpo) };
        req1.Headers.Add("Idempotency-Key", chave);
        var r1 = await ana.SendAsync(req1);
        var req2 = new HttpRequestMessage(HttpMethod.Post, "/relatos") { Content = JsonContent.Create(corpo) };
        req2.Headers.Add("Idempotency-Key", chave);
        var r2 = await ana.SendAsync(req2);

        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);
        Assert.Equal(HttpStatusCode.Created, r2.StatusCode); // mesmo status da 1ª resposta
        Assert.False(r1.Headers.Contains("Idempotent-Replayed"));
        Assert.Equal("true", r2.Headers.GetValues("Idempotent-Replayed").Single());
        var id1 = (await Json(r1)).GetProperty("relato").GetProperty("id").GetInt64();
        var id2 = (await Json(r2)).GetProperty("relato").GetProperty("id").GetInt64();
        Assert.Equal(id1, id2);

        var depois = (await Json(await ana.GetAsync("/relatos/meus?size=50"))).GetProperty("totalElementos").GetInt64();
        Assert.Equal(antes + 1, depois);
        Assert.Single(factory.Eventos.Todos, e => e.Tipo == TipoEvento.CRIADO && e.RelatoId == id1);
    }

    [Fact(DisplayName = "Idempotency-Key: mesma chave com outro conteúdo → 422; chave longa → 400")]
    public async Task IdempotenciaConflito()
    {
        var ana = await factory.ClienteLogadoAsync(DadosTeste.EmailAna);
        var chave = Guid.NewGuid().ToString();

        var (s1, _) = await Enviar(ana, Corpo(LatBase + 0.005), chave);
        var (s2, erro) = await Enviar(ana, Corpo(LatBase + 0.005, texto: "Outro texto"), chave);
        var (s3, longa) = await Enviar(ana, Corpo(LatBase + 0.005), new string('x', 101));

        Assert.Equal(HttpStatusCode.Created, s1);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, s2);
        Assert.Equal("Esta Idempotency-Key já foi usada com outro conteúdo.", erro.GetProperty("mensagem").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, s3);
        Assert.Contains("100 caracteres", longa.GetProperty("mensagem").GetString());
    }

    [Fact(DisplayName = "Idempotency-Key é por usuário: a mesma chave de outra pessoa não devolve o relato alheio")]
    public async Task IdempotenciaPorUsuario()
    {
        var ana = await factory.ClienteLogadoAsync(DadosTeste.EmailAna);
        var bruno = await factory.ClienteLogadoAsync(DadosTeste.EmailBruno);
        const string chave = "mesma-chave-para-os-dois";

        var (_, daAna) = await Enviar(ana, Corpo(LatBase + 0.006), chave);
        var (status, doBruno) = await Enviar(bruno, Corpo(LatBase + 0.006, tipo: "PISO_TATIL"), chave);

        Assert.Equal(HttpStatusCode.Created, status);
        Assert.NotEqual(daAna.GetProperty("relato").GetProperty("id").GetInt64(), doBruno.GetProperty("relato").GetProperty("id").GetInt64());
    }

    [Fact(DisplayName = "POST /relatos: validação (tipo ausente, latitude fora da faixa, enum inválido) → 400")]
    public async Task Validacao()
    {
        var ana = await factory.ClienteLogadoAsync(DadosTeste.EmailAna);

        var r = await ana.PostAsJsonAsync("/relatos", new { gravidade = "ALTA", texto = " ", latitude = 100, longitude = 0 });
        var campos = (await Json(r)).GetProperty("campos").EnumerateArray().Select(c => c.GetProperty("campo").GetString()).ToList();
        var enumInvalido = await ana.PostAsJsonAsync("/relatos", new { tipo = "XYZ", gravidade = "ALTA", texto = "a", latitude = 0, longitude = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("tipo", campos);
        Assert.Contains("texto", campos);
        Assert.Contains("latitude", campos);
        Assert.Equal(HttpStatusCode.BadRequest, enumInvalido.StatusCode);
    }

    [Fact(DisplayName = "GET /relatos/{id}: quem não é autor nem apoiador recebe 404; /meus é paginado")]
    public async Task Acesso()
    {
        var ana = await factory.ClienteLogadoAsync(DadosTeste.EmailAna);
        var bruno = await factory.ClienteLogadoAsync(DadosTeste.EmailBruno);
        var (_, criado) = await Enviar(ana, Corpo(LatBase + 0.007));
        var id = criado.GetProperty("relato").GetProperty("id").GetInt64();

        Assert.Equal(HttpStatusCode.OK, (await ana.GetAsync($"/relatos/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bruno.GetAsync($"/relatos/{id}")).StatusCode);

        var pagina = await Json(await ana.GetAsync("/relatos/meus?page=0&size=1"));
        Assert.Equal(1, pagina.GetProperty("tamanho").GetInt32());
        Assert.Single(pagina.GetProperty("conteudo").EnumerateArray());
        Assert.True(pagina.GetProperty("totalPaginas").GetInt32() >= 1);
    }

    [Fact(DisplayName = "PUT /relatos/{id}: autor edita enquanto ABERTO; outro usuário recebe 404")]
    public async Task Editar()
    {
        var ana = await factory.ClienteLogadoAsync(DadosTeste.EmailAna);
        var bruno = await factory.ClienteLogadoAsync(DadosTeste.EmailBruno);
        var (_, criado) = await Enviar(ana, Corpo(LatBase + 0.008));
        var id = criado.GetProperty("relato").GetProperty("id").GetInt64();
        var edicao = new { tipo = "OBSTRUCAO_PERMANENTE", gravidade = "MEDIA", texto = "Poste no meio da calçada" };

        var ok = await ana.PutAsJsonAsync($"/relatos/{id}", edicao);
        var alheio = await bruno.PutAsJsonAsync($"/relatos/{id}", edicao);

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var json = await Json(ok);
        Assert.Equal("OBSTRUCAO_PERMANENTE", json.GetProperty("tipo").GetString());
        Assert.Equal(3, json.GetProperty("prioridade").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, alheio.StatusCode);
    }

    [Fact(DisplayName = "DELETE /relatos/{id}: sem apoios → 204; com apoios → 409")]
    public async Task Excluir()
    {
        var ana = await factory.ClienteLogadoAsync(DadosTeste.EmailAna);
        var bruno = await factory.ClienteLogadoAsync(DadosTeste.EmailBruno);

        var (_, semApoio) = await Enviar(ana, Corpo(LatBase + 0.009));
        var idSemApoio = semApoio.GetProperty("relato").GetProperty("id").GetInt64();
        Assert.Equal(HttpStatusCode.NoContent, (await ana.DeleteAsync($"/relatos/{idSemApoio}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ana.GetAsync($"/relatos/{idSemApoio}")).StatusCode);

        var (_, comApoio) = await Enviar(ana, Corpo(LatBase + 0.010));
        var idComApoio = comApoio.GetProperty("relato").GetProperty("id").GetInt64();
        await Enviar(bruno, Corpo(LatBase + 0.010));
        var conflito = await ana.DeleteAsync($"/relatos/{idComApoio}");

        Assert.Equal(HttpStatusCode.Conflict, conflito.StatusCode);
        Assert.Equal("Outras pessoas já confirmaram este relato. Ele não pode ser excluído.",
            (await Json(conflito)).GetProperty("mensagem").GetString());
    }

    [Fact(DisplayName = "perfis: gestor não usa /relatos e cidadão não usa /gestor/relatos (403)")]
    public async Task Perfis()
    {
        var gestora = await factory.ClienteLogadoAsync(DadosTeste.EmailMarina);
        var ana = await factory.ClienteLogadoAsync(DadosTeste.EmailAna);

        var r1 = await gestora.PostAsJsonAsync("/relatos", Corpo(LatBase));
        var r2 = await ana.GetAsync("/gestor/relatos");

        Assert.Equal(HttpStatusCode.Forbidden, r1.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, r2.StatusCode);
        Assert.Equal("Você não tem permissão para esta ação.", (await Json(r2)).GetProperty("mensagem").GetString());
    }
}
