using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace VeAI.Tests.Integration;

/// <summary>Fluxo completo: relato → evento → consumidor → notificação para as pessoas certas.</summary>
public class NotificacaoTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<List<JsonElement>> Notificacoes(HttpClient c, string filtro = "") =>
        (await Json(await c.GetAsync("/notificacoes?size=50" + filtro))).GetProperty("conteudo").EnumerateArray().ToList();

    [Fact(DisplayName = "relato novo avisa os gestores da região; apoio avisa o autor; status avisa autor e apoiadores")]
    public async Task FluxoCompleto()
    {
        var ana = await factory.ClienteLogadoAsync(DadosTeste.EmailAna);
        var bruno = await factory.ClienteLogadoAsync(DadosTeste.EmailBruno);
        var marina = await factory.ClienteLogadoAsync(DadosTeste.EmailMarina);
        var rafael = await factory.ClienteLogadoAsync(DadosTeste.EmailRafael);
        var corpo = new { tipo = "PISO_TATIL", gravidade = "MEDIA", texto = "Piso tátil arrancado", latitude = -23.5662, longitude = -46.6850 };

        var criado = await Json(await ana.PostAsJsonAsync("/relatos", corpo));
        var id = criado.GetProperty("relato").GetProperty("id").GetInt64();
        await bruno.PostAsJsonAsync("/relatos", corpo); // apoio
        await marina.PatchAsJsonAsync($"/gestor/relatos/{id}/status", new { status = "EM_TRIAGEM" });

        var daMarina = (await Notificacoes(marina)).Where(n => n.GetProperty("relatoId").GetInt64() == id).ToList();
        Assert.Contains(daMarina, n => n.GetProperty("mensagem").GetString() == "Novo relato de problema no piso tátil em Pinheiros.");
        Assert.DoesNotContain(await Notificacoes(rafael), n => n.GetProperty("relatoId").GetInt64() == id); // outra região

        var daAna = (await Notificacoes(ana)).Where(n => n.GetProperty("relatoId").GetInt64() == id).Select(n => n.GetProperty("mensagem").GetString()).ToList();
        Assert.Contains("Mais uma pessoa confirmou seu relato de problema no piso tátil. Agora 1 pessoa já confirmou.", daAna);
        Assert.Contains("Seu relato de problema no piso tátil está em análise pela rede de apoio.", daAna);

        var doBruno = (await Notificacoes(bruno)).Where(n => n.GetProperty("relatoId").GetInt64() == id).Select(n => n.GetProperty("mensagem").GetString()).ToList();
        Assert.Equal(["O relato de problema no piso tátil que você apoiou está em análise pela rede de apoio."], doBruno);
    }

    [Fact(DisplayName = "marcar como lida, filtrar não lidas e notificação de outra pessoa → 404")]
    public async Task MarcarLida()
    {
        var ana = await factory.ClienteLogadoAsync(DadosTeste.EmailAna);
        var bruno = await factory.ClienteLogadoAsync(DadosTeste.EmailBruno);
        var marina = await factory.ClienteLogadoAsync(DadosTeste.EmailMarina);
        await ana.PostAsJsonAsync("/relatos", new { tipo = "RAMPA", gravidade = "BAIXA", texto = "Rampa", latitude = -23.5503, longitude = -46.6320 });
        var notificacao = (await Notificacoes(marina, "&lida=false")).First();
        var id = notificacao.GetProperty("id").GetInt64();

        Assert.Equal(HttpStatusCode.NotFound, (await bruno.PatchAsync($"/notificacoes/{id}/lida", null)).StatusCode);
        var lida = await Json(await marina.PatchAsync($"/notificacoes/{id}/lida", null));
        Assert.True(lida.GetProperty("lida").GetBoolean());
        Assert.DoesNotContain(await Notificacoes(marina, "&lida=false"), n => n.GetProperty("id").GetInt64() == id);
        Assert.Contains(await Notificacoes(marina, "&lida=true"), n => n.GetProperty("id").GetInt64() == id);
    }

    [Fact(DisplayName = "descarte avisa com o motivo; encaminhamento com o protocolo; sem token → 401")]
    public async Task MensagensDeStatus()
    {
        var ana = await factory.ClienteLogadoAsync(DadosTeste.EmailAna);
        var marina = await factory.ClienteLogadoAsync(DadosTeste.EmailMarina);
        var r1 = await Json(await ana.PostAsJsonAsync("/relatos", new { tipo = "RAMPA", gravidade = "ALTA", texto = "a", latitude = -23.5662, longitude = -46.6800 }));
        var r2 = await Json(await ana.PostAsJsonAsync("/relatos", new { tipo = "RAMPA", gravidade = "ALTA", texto = "b", latitude = -23.5662, longitude = -46.6780 }));
        var id1 = r1.GetProperty("relato").GetProperty("id").GetInt64();
        var id2 = r2.GetProperty("relato").GetProperty("id").GetInt64();

        await marina.PatchAsJsonAsync($"/gestor/relatos/{id1}/status", new { status = "DESCARTADO", motivoDescarte = "Duplicado" });
        await marina.PatchAsJsonAsync($"/gestor/relatos/{id2}/status", new { status = "EM_TRIAGEM" });
        await marina.PatchAsJsonAsync($"/gestor/relatos/{id2}/status", new { status = "ENCAMINHADO", protocoloOrgao = "SP156-9" });

        var mensagens = (await Notificacoes(ana)).Select(n => n.GetProperty("mensagem").GetString()).ToList();
        Assert.Contains("Seu relato de problema na rampa foi encerrado. Motivo: Duplicado", mensagens);
        Assert.Contains("Seu relato de problema na rampa foi encaminhado ao órgão responsável. Protocolo SP156-9.", mensagens);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/notificacoes")).StatusCode);
    }
}
