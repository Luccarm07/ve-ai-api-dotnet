using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VeAI.Application.Abstracoes;
using VeAI.Application.Opcoes;
using VeAI.Domain.Enums;
using VeAI.Domain.Excecoes;
using VeAI.Domain.Modelos;

namespace VeAI.Infrastructure.Ia;

/// <summary>
/// Descrição por IA generativa com a API da OpenAI (Chat Completions): a foto vai como imagem em base64 e a resposta
/// volta em JSON estruturado (json_schema estrito), convertido direto para DescricaoIA. Ativado com Ia__Provedor=openai
/// e OPENAI_API_KEY no .env. Mesmo prompt e mesmos pedidos da API Java.
/// </summary>
public sealed class DescritorOpenAi(HttpClient http, IOptions<IaOptions> opcoes, ILogger<DescritorOpenAi> log) : IDescritorImagem
{
    private const string PedidoAmbiente =
        "Esta foto foi tirada na altura do peito, na direção em que a pessoa anda. " +
        "Descreva o que está à frente dela seguindo as regras. A descricaoCompleta é o texto falado: comece pelos perigos.";
    private const string SemContexto = " Não há FOTO ANTERIOR: use \"novidade\": true e descreva a cena inteira.";
    private const string Confirmacao =
        "Se a cena for a mesma (novidade false), a descricaoCompleta é UMA frase só, de até 100 caracteres, " +
        "sem descrever o lugar de novo. Ex.: \"O carro e o poste continuam no mesmo lugar, à sua frente.\" " +
        "ou \"O carro e o poste estão um pouco mais perto, a cerca de 2 metros.\"";
    private const string PedidoTexto =
        "Leia o texto desta foto exatamente como está escrito e coloque-o em textoLido. " +
        "Em resumo, diga em uma frase do que se trata (placa, cardápio, embalagem, documento...). " +
        "Também informe perigos visíveis, se houver.";
    private const string PedidoPergunta =
        "Responda em texto corrido, em até duas frases, sem inventar nada que não esteja na foto. " +
        "Dê posições a partir de quem segura o celular (à sua frente, à sua direita) e distâncias aproximadas em metros, por extenso. Pergunta: ";

    private static readonly Lazy<string> PromptSistema = new(() =>
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("VeAI.Prompts.descricao-sistema.txt")!;
        return new StreamReader(stream).ReadToEnd();
    });

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string Provedor => "openai";

    public async Task<DescricaoIA> DescreverAsync(ImagemRecebida imagem, ModoDescricao modo, ContextoAnterior? anterior,
                                                  CancellationToken ct = default)
    {
        try
        {
            var corpo = Requisicao(Pedido(modo, anterior), imagem, comEsquema: true);
            var conteudo = await EnviarAsync(corpo, ct);
            return (JsonSerializer.Deserialize<DescricaoIA>(conteudo, Json) ?? throw new JsonException("Resposta vazia")).Normalizada();
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException or InvalidOperationException)
        {
            log.LogWarning("Falha ao descrever imagem ({Imagem}): {Erro}", imagem, e.Message);
            throw new RegraNegocioException(TipoErro.ServicoExterno, "Não consegui descrever a foto agora. Tente de novo em alguns segundos.");
        }
    }

    public async Task<string> ResponderAsync(ImagemRecebida imagem, string pergunta, CancellationToken ct = default)
    {
        try
        {
            return (await EnviarAsync(Requisicao(PedidoPergunta + pergunta, imagem, comEsquema: false), ct)).Trim();
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException or InvalidOperationException)
        {
            log.LogWarning("Falha na pergunta livre ({Imagem}): {Erro}", imagem, e.Message);
            throw new RegraNegocioException(TipoErro.ServicoExterno, "Não consegui responder agora. Tente de novo.");
        }
    }

    /// <summary>Texto do pedido ao modelo (igual ao da API Java), com a foto anterior no modo automático.</summary>
    public static string Pedido(ModoDescricao modo, ContextoAnterior? anterior)
    {
        if (modo == ModoDescricao.TEXTO)
            return PedidoTexto;
        if (anterior is null)
            return PedidoAmbiente + SemContexto;

        var perigos = anterior.Perigos.Count == 0
            ? "nenhum"
            : string.Join("; ", anterior.Perigos.Select(p => $"{Limpar(p.Descricao)} ({Limpar(p.Direcao)}, {Limpar(p.DistanciaAprox)})"));
        return PedidoAmbiente + $"\n\nModo automático. FOTO ANTERIOR (há {anterior.SegundosAtras} segundos):\n" +
               $"- O que a pessoa ouviu: \"{Limpar(anterior.DescricaoCompleta)}\"\n" +
               $"- Cena: \"{Limpar(anterior.Resumo)}\"\n" +
               $"- Perigos: {perigos}\n" +
               "Esta é a foto seguinte da caminhada. Siga as regras de CONTINUIDADE. " + Confirmacao;
    }

    internal JsonObject Requisicao(string pedido, ImagemRecebida imagem, bool comEsquema)
    {
        var config = opcoes.Value;
        var corpo = new JsonObject
        {
            ["model"] = config.Modelo,
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "system", ["content"] = PromptSistema.Value },
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = new JsonArray
                    {
                        new JsonObject { ["type"] = "text", ["text"] = pedido },
                        new JsonObject
                        {
                            ["type"] = "image_url",
                            ["image_url"] = new JsonObject { ["url"] = $"data:{imagem.Tipo};base64,{Convert.ToBase64String(imagem.Bytes)}" }
                        }
                    }
                }
            }
        };
        if (!string.IsNullOrWhiteSpace(config.ReasoningEffort))
            corpo["reasoning_effort"] = config.ReasoningEffort;
        if (comEsquema)
            corpo["response_format"] = new JsonObject
            {
                ["type"] = "json_schema",
                ["json_schema"] = new JsonObject { ["name"] = "descricao_ia", ["strict"] = true, ["schema"] = Esquema() }
            };
        return corpo;
    }

    private async Task<string> EnviarAsync(JsonObject corpo, CancellationToken ct)
    {
        var chave = opcoes.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(chave))
            throw new InvalidOperationException("OPENAI_API_KEY não configurada.");

        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions") { Content = JsonContent.Create(corpo) };
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", chave);
        using var resposta = await http.SendAsync(requisicao, ct);
        if (!resposta.IsSuccessStatusCode)
            throw new HttpRequestException($"OpenAI respondeu {(int)resposta.StatusCode}");

        var json = await resposta.Content.ReadFromJsonAsync<JsonObject>(ct) ?? throw new JsonException("Resposta vazia");
        return json["choices"]?[0]?["message"]?["content"]?.GetValue<string>() ?? throw new JsonException("Resposta sem conteúdo");
    }

    /// <summary>JSON Schema estrito da DescricaoIA: todos os campos obrigatórios, opcionais aceitam null.</summary>
    internal static JsonObject Esquema()
    {
        static JsonObject Texto(bool anulavel = false) => new() { ["type"] = anulavel ? new JsonArray("string", "null") : "string" };
        static JsonObject Enum<T>() where T : struct, System.Enum =>
            new() { ["type"] = new JsonArray("string", "null"), ["enum"] = new JsonArray([.. System.Enum.GetNames<T>().Select(n => (JsonNode)n), null]) };
        static JsonObject Objeto(JsonObject propriedades) => new()
        {
            ["type"] = "object",
            ["properties"] = propriedades,
            ["required"] = new JsonArray([.. propriedades.Select(p => (JsonNode)p.Key)]),
            ["additionalProperties"] = false
        };

        return Objeto(new JsonObject
        {
            ["perigos"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = Objeto(new JsonObject { ["descricao"] = Texto(), ["direcao"] = Texto(true), ["distanciaAprox"] = Texto(true) })
            },
            ["resumo"] = Texto(),
            ["descricaoCompleta"] = Texto(),
            ["novidade"] = new JsonObject { ["type"] = "boolean" },
            ["objetos"] = new JsonObject { ["type"] = "array", ["items"] = Texto() },
            ["textoLido"] = Texto(true),
            ["barreira"] = Objeto(new JsonObject
            {
                ["detectada"] = new JsonObject { ["type"] = "boolean" },
                ["tipo"] = Enum<TipoRelato>(),
                ["gravidade"] = Enum<Gravidade>(),
                ["confianca"] = new JsonObject { ["type"] = "number" },
                ["justificativa"] = Texto(true)
            })
        });
    }

    private static string Limpar(string? texto) => texto is null ? "" : texto.Replace("{", "").Replace("}", "").Replace("\"", "");
}
