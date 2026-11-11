using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using VeAI.Domain.Eventos;

namespace VeAI.Tests.Integration;

public class GestorTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<long> CriarRelato(double lat, double lon, string gravidade = "MEDIA")
    {
        var ana = await factory.ClienteLogadoAsync(DadosTeste.EmailAna);
        var r = await ana.PostAsJsonAsync("/relatos",
            new { tipo = "RAMPA", gravidade, texto = "Rampa quebrada", latitude = lat, longitude = lon });
        return (await Json(r)).GetProperty("relato").GetProperty("id").GetInt64();
    }

    [Fact(DisplayName = "fila do gestor: só regiões atendidas, por prioridade; regiaoId alheio → 403")]
    public async Task Fila()
    {
        var emPinheiros = await CriarRelato(-23.5662, -46.6890, "ALTA");
        var naSe = await CriarRelato(-23.5503, -46.6339, "BAIXA");
        var naVilaMariana = await CriarRelato(-23.5889, -46.6355, "ALTA");
        var marina = await factory.ClienteLogadoAsync(DadosTeste.EmailMarina);

        var fila = await Json(await marina.GetAsync("/gestor/relatos?size=50"));
        var conteudo = fila.GetProperty("conteudo").EnumerateArray().ToList();
        var ids = conteudo.Select(r => r.GetProperty("id").GetInt64()).ToList();

        Assert.Contains(emPinheiros, ids);
        Assert.Contains(naSe, ids);
        Assert.DoesNotContain(naVilaMariana, ids);
        var prioridades = conteudo.Select(r => r.GetProperty("prioridade").GetInt32()).ToList();
        Assert.Equal(prioridades.OrderByDescending(p => p), prioridades);

        var regioes = await Json(await marina.GetAsync("/regioes"));
        var vilaMariana = regioes.EnumerateArray().Single(r => r.GetProperty("nome").GetString() == "Vila Mariana").GetProperty("id").GetInt64();
        Assert.Equal(HttpStatusCode.Forbidden, (await marina.GetAsync($"/gestor/relatos?regiaoId={vilaMariana}")).StatusCode);

        var soAbertos = await Json(await marina.GetAsync("/gestor/relatos?status=ABERTO&size=50"));
        Assert.All(soAbertos.GetProperty("conteudo").EnumerateArray(), r => Assert.Equal("ABERTO", r.GetProperty("status").GetString()));
    }

    [Fact(DisplayName = "gestor de outra região não vê nem altera o relato (403)")]
    public async Task OutraRegiao()
    {
        var id = await CriarRelato(-23.5662, -46.6880);
        var rafael = await factory.ClienteLogadoAsync(DadosTeste.EmailRafael);

        var ver = await rafael.GetAsync($"/gestor/relatos/{id}");
        var mudar = await rafael.PatchAsJsonAsync($"/gestor/relatos/{id}/status", new { status = "EM_TRIAGEM" });

        Assert.Equal(HttpStatusCode.Forbidden, ver.StatusCode);
        Assert.Equal("Este relato é de uma região que você não atende.", (await Json(ver)).GetProperty("mensagem").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, mudar.StatusCode);
    }

    [Fact(DisplayName = "máquina de estados: triagem → (pular = 422) → encaminhar exige protocolo → histórico e evento")]
    public async Task MaquinaDeEstados()
    {
        var id = await CriarRelato(-23.5662, -46.6870, "ALTA");
        var marina = await factory.ClienteLogadoAsync(DadosTeste.EmailMarina);

        var triagem = await marina.PatchAsJsonAsync($"/gestor/relatos/{id}/status", new { status = "EM_TRIAGEM", comentario = "Vistoria marcada" });
        Assert.Equal(HttpStatusCode.OK, triagem.StatusCode);
        Assert.Equal("EM_TRIAGEM", (await Json(triagem)).GetProperty("status").GetString());

        var pular = await marina.PatchAsJsonAsync($"/gestor/relatos/{id}/status", new { status = "RESOLVIDO" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, pular.StatusCode);
        Assert.Equal("Não é possível mudar o relato de EM_TRIAGEM para RESOLVIDO.", (await Json(pular)).GetProperty("mensagem").GetString());

        var semProtocolo = await marina.PatchAsJsonAsync($"/gestor/relatos/{id}/status", new { status = "ENCAMINHADO" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, semProtocolo.StatusCode);

        var encaminhado = await marina.PatchAsJsonAsync($"/gestor/relatos/{id}/status",
            new { status = "ENCAMINHADO", protocoloOrgao = "SP156-2026-0012345" });
        Assert.Equal("SP156-2026-0012345", (await Json(encaminhado)).GetProperty("protocoloOrgao").GetString());

        var historico = (await Json(await marina.GetAsync($"/gestor/relatos/{id}/historico"))).EnumerateArray().ToList();
        Assert.Equal(2, historico.Count);
        Assert.Equal("ABERTO", historico[0].GetProperty("statusAnterior").GetString());
        Assert.Equal("Vistoria marcada", historico[0].GetProperty("comentario").GetString());
        Assert.Equal("Marina Alves", historico[1].GetProperty("gestorNome").GetString());
        Assert.Equal(2, factory.Eventos.Todos.Count(e => e.Tipo == TipoEvento.STATUS && e.RelatoId == id));

        // Relato fora de ABERTO não pode mais ser editado nem excluído pelo autor (422).
        var ana = await factory.ClienteLogadoAsync(DadosTeste.EmailAna);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await ana.DeleteAsync($"/relatos/{id}")).StatusCode);
    }

    [Fact(DisplayName = "descartar exige motivo e zera a prioridade; status inválido → 400; relato inexistente → 404")]
    public async Task Descartar()
    {
        var id = await CriarRelato(-23.5662, -46.6860);
        var marina = await factory.ClienteLogadoAsync(DadosTeste.EmailMarina);

        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await marina.PatchAsJsonAsync($"/gestor/relatos/{id}/status", new { status = "DESCARTADO" })).StatusCode);
        var descartado = await Json(await marina.PatchAsJsonAsync($"/gestor/relatos/{id}/status",
            new { status = "DESCARTADO", motivoDescarte = "Não encontrado na vistoria" }));
        Assert.Equal(0, descartado.GetProperty("prioridade").GetInt32());

        Assert.Equal(HttpStatusCode.BadRequest,
            (await marina.PatchAsJsonAsync($"/gestor/relatos/{id}/status", new { status = "XYZ" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await marina.PatchAsJsonAsync($"/gestor/relatos/{id}/status", new { comentario = "sem status" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await marina.GetAsync("/gestor/relatos/999999")).StatusCode);
    }
}
