using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using VeAI.Infrastructure.Persistence;

namespace VeAI.Tests.Unit.Infrastructure;

/// <summary>
/// Confere o mapeamento do EF Core contra os scripts Oracle do repositório (database/oracle):
/// toda tabela e coluna mapeada existe no DDL e toda coluna do DDL está mapeada.
/// Pega erro de nome de coluna sem precisar de um Oracle rodando.
/// </summary>
public partial class MapeamentoOracleTests
{
    private static readonly Dictionary<string, HashSet<string>> Ddl = LerDdl();

    private static IModel ModeloOracle()
    {
        var options = new DbContextOptionsBuilder<VeAiDbContext>()
            .UseOracle("User Id=x;Password=x;Data Source=localhost/XEPDB1") // não abre conexão: só monta o modelo
            .Options;
        using var db = new VeAiDbContext(options);
        return db.GetService<IDesignTimeModel>().Model;
    }

    [Fact(DisplayName = "toda tabela e coluna mapeada existe no DDL Oracle")]
    public void MapeadoExisteNoDdl()
    {
        foreach (var entidade in ModeloOracle().GetEntityTypes())
        {
            var tabela = entidade.GetTableName()!;
            Assert.True(Ddl.ContainsKey(tabela), $"Tabela {tabela} não existe no DDL.");
            var objeto = StoreObjectIdentifier.Table(tabela, null);
            foreach (var propriedade in entidade.GetProperties())
            {
                var coluna = propriedade.GetColumnName(objeto)!;
                Assert.True(Ddl[tabela].Contains(coluna), $"Coluna {tabela}.{coluna} não existe no DDL.");
            }
        }
    }

    [Fact(DisplayName = "toda coluna do DDL está mapeada (exceto a auditoria, escrita só pelo gatilho)")]
    public void DdlEstaMapeado()
    {
        var modelo = ModeloOracle();
        foreach (var (tabela, colunas) in Ddl.Where(t => t.Key != "AUDITORIA_RELATO"))
        {
            var entidade = modelo.GetEntityTypes().SingleOrDefault(e => e.GetTableName() == tabela);
            Assert.True(entidade is not null, $"Tabela {tabela} do DDL não está mapeada.");
            var objeto = StoreObjectIdentifier.Table(tabela, null);
            var mapeadas = entidade!.GetProperties().Select(p => p.GetColumnName(objeto)).ToHashSet();
            foreach (var coluna in colunas)
                Assert.True(mapeadas.Contains(coluna), $"Coluna {tabela}.{coluna} não está mapeada.");
        }
    }

    private static Dictionary<string, HashSet<string>> LerDdl()
    {
        var pasta = LocalizarPastaDatabase();
        var sql = File.ReadAllText(Path.Combine(pasta, "01_schema.sql")) + File.ReadAllText(Path.Combine(pasta, "03_extensoes_api.sql"));
        var tabelas = new Dictionary<string, HashSet<string>>();

        foreach (Match tabela in CreateTable().Matches(sql))
        {
            var colunas = Coluna().Matches(tabela.Groups["corpo"].Value).Select(m => m.Groups[1].Value.ToUpperInvariant());
            tabelas[tabela.Groups["nome"].Value.ToUpperInvariant()] = colunas.ToHashSet();
        }

        // V3: ALTER TABLE regiao ADD (latitude ..., longitude ...)
        tabelas["REGIAO"].UnionWith(["LATITUDE", "LONGITUDE"]);
        return tabelas;
    }

    private static string LocalizarPastaDatabase()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var caminho = Path.Combine(dir.FullName, "database", "oracle");
            if (Directory.Exists(caminho))
                return caminho;
        }
        throw new DirectoryNotFoundException("Pasta database/oracle não encontrada.");
    }

    [GeneratedRegex(@"CREATE TABLE (?<nome>\w+) \((?<corpo>.*?)\n\s*\)'\);", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex CreateTable();

    [GeneratedRegex(@"^\s{6}(\w+)\s+(?:NUMBER|VARCHAR2|CLOB|NCLOB|DATE|CHAR|TIMESTAMP)", RegexOptions.Multiline)]
    private static partial Regex Coluna();
}
