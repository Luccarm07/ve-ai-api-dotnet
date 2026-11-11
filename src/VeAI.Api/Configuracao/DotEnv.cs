namespace VeAI.Api.Configuracao;

/// <summary>
/// Carrega o arquivo .env (se existir) para as variáveis de ambiente, antes de o ASP.NET Core ler a configuração.
/// Variáveis já definidas no sistema têm prioridade. O .env nunca vai para o git.
/// </summary>
public static class DotEnv
{
    public static void Carregar(string? diretorioInicial = null)
    {
        var arquivo = Localizar(diretorioInicial ?? Directory.GetCurrentDirectory());
        if (arquivo is null)
            return;

        foreach (var linhaBruta in File.ReadAllLines(arquivo))
        {
            var linha = linhaBruta.Trim();
            if (linha.Length == 0 || linha.StartsWith('#'))
                continue;

            var igual = linha.IndexOf('=');
            if (igual <= 0)
                continue;

            var chave = linha[..igual].Trim();
            var valor = linha[(igual + 1)..].Trim().Trim('"');
            if (Environment.GetEnvironmentVariable(chave) is null)
                Environment.SetEnvironmentVariable(chave, valor);
        }
    }

    /// <summary>Procura o .env na pasta atual e em até 3 pastas acima (raiz do repositório ao rodar pela IDE).</summary>
    private static string? Localizar(string diretorio)
    {
        var atual = new DirectoryInfo(diretorio);
        for (var i = 0; i < 4 && atual is not null; i++, atual = atual.Parent)
        {
            var caminho = Path.Combine(atual.FullName, ".env");
            if (File.Exists(caminho))
                return caminho;
        }
        return null;
    }
}
