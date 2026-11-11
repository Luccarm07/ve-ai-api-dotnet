using System.Net.Http.Json;
using System.Text.Json;
using YamlDotNet.Serialization;

namespace VeAI.Tests.Integration;

/// <summary>
/// O app mobile consome a API Java e a .NET. Este teste compara o OpenAPI gerado aqui com o contrato da API Java
/// (docs/openapi-contrato-java.yaml): mesmas rotas, mesmos métodos e os mesmos campos em cada schema.
/// </summary>
public class ContratoTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly string[] Metodos = ["get", "post", "put", "patch", "delete"];

    [Fact(DisplayName = "contrato: todas as rotas e métodos da API Java existem aqui, e nenhuma a mais")]
    public async Task Rotas()
    {
        var (java, dotnet) = await Documentos();

        var deJava = Operacoes((Dictionary<object, object>)java["paths"]);
        var daqui = dotnet.GetProperty("paths").EnumerateObject()
            .SelectMany(p => p.Value.EnumerateObject().Where(m => Metodos.Contains(m.Name)).Select(m => $"{m.Name.ToUpper()} {p.Name}"))
            .ToHashSet();

        Assert.Equal(22, deJava.Count);
        Assert.Equal(deJava.Order(), daqui.Order());
    }

    [Fact(DisplayName = "contrato: cada schema da API Java tem exatamente os mesmos campos aqui")]
    public async Task Schemas()
    {
        var (java, dotnet) = await Documentos();
        var esquemasJava = (Dictionary<object, object>)((Dictionary<object, object>)java["components"])["schemas"];
        var esquemasDotnet = dotnet.GetProperty("components").GetProperty("schemas");

        foreach (var (nomeObj, corpo) in esquemasJava)
        {
            var nome = (string)nomeObj;
            // Genéricos: PaginaRespostaRelatoResposta (Java) = PaginaRespostaOfRelatoResposta (.NET)
            var nomeDotnet = nome.StartsWith("PaginaResposta") ? nome.Replace("PaginaResposta", "PaginaRespostaOf") : nome;
            Assert.True(esquemasDotnet.TryGetProperty(nomeDotnet, out var esquema), $"Schema {nome} não existe na API .NET.");

            var camposJava = ((Dictionary<object, object>)((Dictionary<object, object>)corpo)["properties"]).Keys.Cast<string>().Order();
            var camposDotnet = esquema.GetProperty("properties").EnumerateObject().Select(p => p.Name).Order();
            Assert.True(camposJava.SequenceEqual(camposDotnet), $"Schema {nome}: Java [{string.Join(", ", camposJava)}] × .NET [{string.Join(", ", camposDotnet)}]");
        }
    }

    private async Task<(Dictionary<object, object> Java, JsonElement Dotnet)> Documentos()
    {
        var caminho = Path.Combine(PastaRaiz(), "docs", "openapi-contrato-java.yaml");
        var java = new DeserializerBuilder().Build().Deserialize<Dictionary<object, object>>(File.ReadAllText(caminho));
        var dotnet = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        return (java, dotnet);
    }

    private static HashSet<string> Operacoes(Dictionary<object, object> paths) =>
        paths.SelectMany(p => ((Dictionary<object, object>)p.Value).Keys.Cast<string>().Where(Metodos.Contains)
            .Select(m => $"{m.ToUpper()} {p.Key}")).ToHashSet();

    private static string PastaRaiz()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "VeAI.sln")))
                return dir.FullName;
        throw new DirectoryNotFoundException("Raiz do repositório não encontrada.");
    }
}
