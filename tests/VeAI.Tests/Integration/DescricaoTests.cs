using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace VeAI.Tests.Integration;

public class DescricaoTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<JsonElement> Descrever(HttpClient c, int cenario, string query = "", params (string, string)[] campos)
    {
        var r = await c.PostAsync("/descricoes" + query, Fotos.Formulario(Fotos.Jpeg(cenario), campos: campos));
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        return await Json(r);
    }

    [Fact(DisplayName = "POST /descricoes: perigos primeiro e sugestão de relato para barreira com confiança alta")]
    public async Task DescreverComBarreira()
    {
        var ana = await factory.ClienteLogadoAsync(DadosTeste.EmailAna);

        var r = await ana.PostAsync("/descricoes", Fotos.Formulario(Fotos.Jpeg(Fotos.Poste)));

        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var d = await Json(r);
        Assert.Equal($"/descricoes/{d.GetProperty("id").GetInt64()}", r.Headers.Location?.ToString());
        Assert.Equal("AMBIENTE", d.GetProperty("modo").GetString());
        Assert.StartsWith("Bem à sua frente, a cerca de 3 metros, há um poste", d.GetProperty("descricaoCompleta").GetString());
        Assert.Equal("frente", d.GetProperty("perigos")[0].GetProperty("direcao").GetString());
        Assert.True(d.GetProperty("novidade").GetBoolean());
        Assert.True(d.GetProperty("barreiraDetectada").GetBoolean());
        var sugestao = d.GetProperty("sugestaoRelato");
        Assert.Equal("OBSTRUCAO_PERMANENTE", sugestao.GetProperty("tipo").GetString());
        Assert.Equal("Parece haver calçada obstruída. Quer avisar a rede de apoio?", sugestao.GetProperty("pergunta").GetString());
    }

    [Theory(DisplayName = "sem sugestão quando não há barreira ou a confiança é baixa (foto escura)")]
    [InlineData(Fotos.Bicicleta, false)]
    [InlineData(Fotos.Escura, true)]
    public async Task SemSugestao(int cenario, bool barreira)
    {
        var bruno = await factory.ClienteLogadoAsync(DadosTeste.EmailBruno);

        var d = await Descrever(bruno, cenario);

        Assert.Equal(barreira, d.GetProperty("barreiraDetectada").GetBoolean());
        Assert.Equal(JsonValueKind.Null, d.GetProperty("sugestaoRelato").ValueKind);
    }

    [Fact(DisplayName = "modo TEXTO (campo do formulário): a fala é o texto lido da placa")]
    public async Task ModoTexto()
    {
        var ana = await factory.ClienteLogadoAsync(DadosTeste.EmailAna);

        var d = await Descrever(ana, Fotos.Poste, "", ("modo", "TEXTO"));

        Assert.Equal("TEXTO", d.GetProperty("modo").GetString());
        Assert.Equal("Farmácia São Paulo. Aberta 24 horas. Entrada pela lateral.", d.GetProperty("descricaoCompleta").GetString());
    }

    [Fact(DisplayName = "modo automático: mesma cena = confirmação curta sem repetir a sugestão; cena nova = novidade")]
    public async Task ModoAutomatico()
    {
        var bruno = await factory.ClienteLogadoAsync(DadosTeste.EmailBruno);
        var primeira = await Descrever(bruno, Fotos.Poste);

        var mesma = await Descrever(bruno, Fotos.Poste, $"?anteriorId={primeira.GetProperty("id").GetInt64()}");
        Assert.False(mesma.GetProperty("novidade").GetBoolean());
        Assert.Equal("Poste, muro e lixeira continuam no mesmo lugar, à sua frente.", mesma.GetProperty("descricaoCompleta").GetString());
        Assert.Equal(JsonValueKind.Null, mesma.GetProperty("sugestaoRelato").ValueKind); // não repete em 2 min

        var nova = await Descrever(bruno, Fotos.Buraco, "", ("anteriorId", mesma.GetProperty("id").GetInt64().ToString()));
        Assert.True(nova.GetProperty("novidade").GetBoolean());
        Assert.Equal("CALCADA_DANIFICADA", nova.GetProperty("sugestaoRelato").GetProperty("tipo").GetString());
    }

    [Fact(DisplayName = "anteriorId de outra pessoa → 404")]
    public async Task AnteriorAlheio()
    {
        var ana = await factory.ClienteLogadoAsync(DadosTeste.EmailAna);
        var bruno = await factory.ClienteLogadoAsync(DadosTeste.EmailBruno);
        var daAna = await Descrever(ana, Fotos.Bicicleta);

        var r = await bruno.PostAsync($"/descricoes?anteriorId={daAna.GetProperty("id").GetInt64()}", Fotos.Formulario(Fotos.Jpeg(0)));

        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.Equal("Descrição anterior não encontrada.", (await Json(r)).GetProperty("mensagem").GetString());
    }

    [Fact(DisplayName = "foto inválida: sem arquivo → 400, GIF → 415, acima de 5 MB → 413, JSON em vez de multipart → 415")]
    public async Task FotoInvalida()
    {
        var ana = await factory.ClienteLogadoAsync(DadosTeste.EmailAna);

        var semFoto = await ana.PostAsync("/descricoes", new MultipartFormDataContent { { new StringContent("AMBIENTE"), "modo" } });
        var gif = await ana.PostAsync("/descricoes", Fotos.Formulario(Encoding.ASCII.GetBytes("GIF89a....."), "image/gif"));
        var grande = new byte[5 * 1024 * 1024 + 1];
        grande[0] = 0xFF; grande[1] = 0xD8; grande[2] = 0xFF;
        var muitoGrande = await ana.PostAsync("/descricoes", Fotos.Formulario(grande));
        var json = await ana.PostAsJsonAsync("/descricoes", new { imagem = "abc" });

        Assert.Equal(HttpStatusCode.BadRequest, semFoto.StatusCode);
        Assert.Equal("Envie a foto no campo imagem.", (await Json(semFoto)).GetProperty("mensagem").GetString());
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, gif.StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, muitoGrande.StatusCode);
        Assert.Equal("A foto passa de 5 MB. Envie uma foto menor.", (await Json(muitoGrande)).GetProperty("mensagem").GetString());
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, json.StatusCode);
    }

    [Fact(DisplayName = "histórico: listar, detalhar, avaliar e excluir; descrição de outra pessoa → 404")]
    public async Task Historico()
    {
        var ana = await factory.ClienteLogadoAsync(DadosTeste.EmailAna);
        var bruno = await factory.ClienteLogadoAsync(DadosTeste.EmailBruno);
        var id = (await Descrever(ana, Fotos.Bicicleta)).GetProperty("id").GetInt64();

        var lista = await Json(await ana.GetAsync("/descricoes?size=1"));
        Assert.Equal(id, lista.GetProperty("conteudo")[0].GetProperty("id").GetInt64()); // mais recente primeiro
        var detalhe = await Json(await ana.GetAsync($"/descricoes/{id}"));
        Assert.Equal(JsonValueKind.Null, detalhe.GetProperty("novidade").ValueKind); // só existe no POST
        Assert.Equal(HttpStatusCode.NotFound, (await bruno.GetAsync($"/descricoes/{id}")).StatusCode);

        var avaliada = await Json(await ana.PatchAsJsonAsync($"/descricoes/{id}/avaliacao", new { util = false }));
        Assert.False(avaliada.GetProperty("util").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await ana.PatchAsJsonAsync($"/descricoes/{id}/avaliacao", new { })).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await ana.DeleteAsync($"/descricoes/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ana.GetAsync($"/descricoes/{id}")).StatusCode);
    }

    [Fact(DisplayName = "relato criado a partir da descrição continua existindo quando a descrição é excluída")]
    public async Task RelatoSobreviveADescricao()
    {
        var ana = await factory.ClienteLogadoAsync(DadosTeste.EmailAna);
        var descricao = await Descrever(ana, Fotos.Buraco);
        var id = descricao.GetProperty("id").GetInt64();
        var relato = await Json(await ana.PostAsJsonAsync("/relatos", new
        {
            tipo = "CALCADA_DANIFICADA", gravidade = "ALTA", texto = "Buraco visto pela IA",
            latitude = -23.5501, longitude = -46.6336, descricaoId = id
        }));
        var relatoId = relato.GetProperty("relato").GetProperty("id").GetInt64();
        Assert.Equal(id, relato.GetProperty("relato").GetProperty("descricaoId").GetInt64());

        await ana.DeleteAsync($"/descricoes/{id}");

        var depois = await Json(await ana.GetAsync($"/relatos/{relatoId}"));
        Assert.Equal(JsonValueKind.Null, depois.GetProperty("descricaoId").ValueKind);
    }

    [Fact(DisplayName = "pergunta livre: exige a preferência (403); ativa, responde; pergunta vazia → 400")]
    public async Task PerguntaLivre()
    {
        var bruno = await factory.ClienteLogadoAsync(DadosTeste.EmailBruno);
        var id = (await Descrever(bruno, Fotos.Poste)).GetProperty("id").GetInt64();
        await bruno.PutAsJsonAsync("/usuarios/me/preferencias", new { velocidadeVoz = 1.0, perguntaLivreAtiva = false });

        var bloqueada = await bruno.PostAsync($"/descricoes/{id}/pergunta?pergunta=Tem degrau?", Fotos.Formulario(Fotos.Jpeg(0)));
        Assert.Equal(HttpStatusCode.Forbidden, bloqueada.StatusCode);
        Assert.Equal("Ative a pergunta livre por voz nas preferências.", (await Json(bloqueada)).GetProperty("mensagem").GetString());

        await bruno.PutAsJsonAsync("/usuarios/me/preferencias", new { velocidadeVoz = 1.0, perguntaLivreAtiva = true });
        var ok = await bruno.PostAsync($"/descricoes/{id}/pergunta", Fotos.Formulario(Fotos.Jpeg(0), campos: ("pergunta", "Tem degrau?")));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Contains("degraus", (await Json(ok)).GetProperty("resposta").GetString());

        var vazia = await bruno.PostAsync($"/descricoes/{id}/pergunta", Fotos.Formulario(Fotos.Jpeg(0)));
        Assert.Equal(HttpStatusCode.BadRequest, vazia.StatusCode);
    }

    [Fact(DisplayName = "gestor não usa /descricoes (403); /health mostra a IA")]
    public async Task PerfilEHealth()
    {
        var gestora = await factory.ClienteLogadoAsync(DadosTeste.EmailMarina);

        Assert.Equal(HttpStatusCode.Forbidden, (await gestora.PostAsync("/descricoes", Fotos.Formulario(Fotos.Jpeg(0)))).StatusCode);

        var saude = await Json(await factory.CreateClient().GetAsync("/health"));
        var ia = saude.GetProperty("verificacoes").EnumerateArray().Single(v => v.GetProperty("nome").GetString() == "ia");
        Assert.Equal("Healthy", ia.GetProperty("status").GetString());
        Assert.Contains("modo falso", ia.GetProperty("descricao").GetString());
    }
}
