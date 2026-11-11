using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VeAI.Application.Opcoes;
using VeAI.Domain.Enums;
using VeAI.Domain.Excecoes;
using VeAI.Domain.Modelos;
using VeAI.Infrastructure.Ia;

namespace VeAI.Tests.Unit.Infrastructure;

/// <summary>Cliente da OpenAI testado com um HttpMessageHandler falso: sem rede, sem chave real, sem custo.</summary>
public class DescritorOpenAiTests
{
    private static readonly ImagemRecebida Foto = new([0xFF, 0xD8, 0xFF, 0xE0], "image/jpeg");

    private sealed class HandlerFalso(HttpStatusCode status, string corpo) : HttpMessageHandler
    {
        public JsonObject? Enviado { get; private set; }
        public string? Autorizacao { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Autorizacao = request.Headers.Authorization?.ToString();
            Enviado = JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!.AsObject();
            return new HttpResponseMessage(status) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };
        }
    }

    private static (DescritorOpenAi, HandlerFalso) Criar(HttpStatusCode status, string conteudoModelo, string? chave = "sk-teste")
    {
        var resposta = new JsonObject
        {
            ["choices"] = new JsonArray(new JsonObject { ["message"] = new JsonObject { ["content"] = conteudoModelo } })
        };
        var handler = new HandlerFalso(status, resposta.ToJsonString());
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com") };
        var opcoes = Options.Create(new IaOptions { Provedor = "openai", Modelo = "gpt-6-luna", ApiKey = chave });
        return (new DescritorOpenAi(http, opcoes, NullLogger<DescritorOpenAi>.Instance), handler);
    }

    private const string RespostaModelo = """
        {"perigos":[{"descricao":"Degrau para baixo","direcao":"frente","distanciaAprox":"1 metro"}],
         "resumo":"Escada à frente","descricaoCompleta":"À sua frente, a 1 metro, um degrau para baixo.","novidade":true,
         "objetos":["escada"],"textoLido":null,
         "barreira":{"detectada":true,"tipo":"RAMPA","gravidade":"ALTA","confianca":0.9,"justificativa":"Sem rampa"}}
        """;

    [Fact(DisplayName = "OpenAI: envia prompt, foto em base64 e json_schema estrito; converte a resposta")]
    public async Task Descreve()
    {
        var (descritor, handler) = Criar(HttpStatusCode.OK, RespostaModelo);

        var ia = await descritor.DescreverAsync(Foto, ModoDescricao.AMBIENTE, null);

        Assert.Equal("Bearer sk-teste", handler.Autorizacao);
        Assert.Equal("gpt-6-luna", handler.Enviado!["model"]!.GetValue<string>());
        Assert.StartsWith("Você é o Vê AI", handler.Enviado["messages"]![0]!["content"]!.GetValue<string>());
        var usuario = handler.Enviado["messages"]![1]!["content"]!.AsArray();
        Assert.Contains("Não há FOTO ANTERIOR", usuario[0]!["text"]!.GetValue<string>());
        Assert.StartsWith("data:image/jpeg;base64,", usuario[1]!["image_url"]!["url"]!.GetValue<string>());
        Assert.True(handler.Enviado["response_format"]!["json_schema"]!["strict"]!.GetValue<bool>());

        Assert.Equal("Degrau para baixo", ia.Perigos![0].Descricao);
        Assert.Equal(TipoRelato.RAMPA, ia.Barreira!.Tipo);
        Assert.Equal(0.9, ia.Barreira.Confianca);
    }

    [Theory(DisplayName = "OpenAI fora do ar, resposta inválida ou sem chave → 502 com mensagem para o usuário")]
    [InlineData(HttpStatusCode.InternalServerError, "{}", "sk-teste")]
    [InlineData(HttpStatusCode.OK, "isto não é json", "sk-teste")]
    [InlineData(HttpStatusCode.OK, "{}", null)]
    public async Task Falhas(HttpStatusCode status, string conteudo, string? chave)
    {
        var (descritor, _) = Criar(status, conteudo, chave);

        var erro = await Assert.ThrowsAsync<RegraNegocioException>(() => descritor.DescreverAsync(Foto, ModoDescricao.AMBIENTE, null));

        Assert.Equal(TipoErro.ServicoExterno, erro.Tipo);
        Assert.StartsWith("Não consegui descrever a foto agora", erro.Message);
    }

    [Fact(DisplayName = "pedido do modo automático inclui a foto anterior; modo TEXTO pede leitura literal")]
    public void Pedidos()
    {
        var anterior = new ContextoAnterior("Há um \"poste\" {à frente}.", "Poste", [new Perigo("Poste", "frente", "3 metros")], 8);

        var automatico = DescritorOpenAi.Pedido(ModoDescricao.AMBIENTE, anterior);

        Assert.Contains("FOTO ANTERIOR (há 8 segundos)", automatico);
        Assert.Contains("Poste (frente, 3 metros)", automatico);
        Assert.Contains("Há um poste à frente.", automatico); // chaves e aspas removidas
        Assert.StartsWith("Leia o texto desta foto", DescritorOpenAi.Pedido(ModoDescricao.TEXTO, null));
    }

    [Fact(DisplayName = "esquema JSON estrito: todos os campos obrigatórios e enums com os valores do domínio")]
    public void Esquema()
    {
        var esquema = DescritorOpenAi.Esquema();

        Assert.Equal(7, esquema["required"]!.AsArray().Count);
        Assert.False(esquema["additionalProperties"]!.GetValue<bool>());
        var tipos = esquema["properties"]!["barreira"]!["properties"]!["tipo"]!["enum"]!.AsArray();
        Assert.Contains(tipos, t => t?.GetValue<string>() == "CALCADA_DANIFICADA");
        Assert.Contains(tipos, t => t is null);
    }

    [Fact(DisplayName = "pergunta livre devolve o texto do modelo, sem esquema")]
    public async Task Pergunta()
    {
        var (descritor, handler) = Criar(HttpStatusCode.OK, "  Não há degraus à sua frente.  ");

        var resposta = await descritor.ResponderAsync(Foto, "Tem degrau?");

        Assert.Equal("Não há degraus à sua frente.", resposta);
        Assert.Null(handler.Enviado!["response_format"]);
        Assert.EndsWith("Pergunta: Tem degrau?", handler.Enviado["messages"]![1]!["content"]![0]!["text"]!.GetValue<string>());
    }
}
