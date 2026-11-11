using Microsoft.EntityFrameworkCore;
using Oracle.EntityFrameworkCore.Infrastructure;
using VeAI.Domain.Entities;
using VeAI.Domain.Enums;
using VeAI.Domain.Modelos;
using VeAI.Infrastructure.Persistence;

namespace VeAI.Tests.Integration;

/// <summary>
/// Roda só quando existe um Oracle com o schema do Vê AI (ex.: "docker compose up -d oracle")
/// e a variável VEAI_ORACLE_TESTE tem a connection string. Sem ela, os testes aparecem como "Skipped".
/// Tudo acontece dentro de uma transação desfeita no fim: o banco não é alterado.
/// </summary>
public class OracleTests
{
    private static VeAiDbContext NovoContexto() =>
        new(new DbContextOptionsBuilder<VeAiDbContext>()
            .UseOracle(OracleFactAttribute.ConnectionString,
                o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19))
            .Options);

    [OracleFact(DisplayName = "Oracle: lê todas as tabelas do seed pelo mapeamento do EF Core")]
    public async Task LeSeedCompleto()
    {
        await using var db = NovoContexto();

        var usuarios = await db.Usuarios.Include(u => u.Regioes).ToListAsync();
        Assert.Contains(usuarios, u => u.Email == "ana.souza@veai.dev" && u.Papel == Papel.CIDADAO);
        Assert.True(usuarios.Single(u => u.Email == "marina.alves@ong-veai.dev").Regioes.Count >= 1);

        var descricoes = await db.Descricoes.ToListAsync();
        Assert.NotEmpty(descricoes);
        Assert.Contains(descricoes, d => d.Perigos.Count > 0);

        var relatos = await db.Relatos.Include(r => r.Regiao).ToListAsync();
        Assert.NotEmpty(relatos);
        Assert.All(relatos, r => Assert.Equal(DateTimeKind.Utc, r.CriadoEm.Kind));

        Assert.NotEmpty(await db.Apoios.ToListAsync());
        Assert.NotEmpty(await db.Atualizacoes.ToListAsync());
        Assert.NotEmpty(await db.Notificacoes.ToListAsync());
        await db.Idempotencias.ToListAsync();
        Assert.All(await db.Regioes.ToListAsync(), r => Assert.True(r.TemCentro));
    }

    [OracleFact(DisplayName = "Oracle: grava relato, apoio (com o gatilho trg_conta_apoio), histórico, CLOB e NCLOB")]
    public async Task GravaFluxoCompleto()
    {
        await using var db = NovoContexto();
        await using var transacao = await db.Database.BeginTransactionAsync();
        var agora = DateTime.UtcNow;

        var ana = await db.Usuarios.SingleAsync(u => u.Email == "ana.souza@veai.dev");
        var bruno = await db.Usuarios.SingleAsync(u => u.Email == "bruno.lima@veai.dev");
        var marina = await db.Usuarios.SingleAsync(u => u.Email == "marina.alves@ong-veai.dev");
        var regiao = await db.Regioes.FirstAsync();

        var descricao = new Descricao(ana.Id, ModoDescricao.AMBIENTE, "Teste Oracle", new string('x', 5000),
            [new Perigo("degrau", "à frente", "1 metro")], ["banco"], true, TipoRelato.RAMPA, 120, agora);
        db.Descricoes.Add(descricao);
        var relato = Relato.Novo(ana.Id, null, regiao, TipoRelato.RAMPA, Gravidade.MEDIA, "Rampa quebrada (teste)",
            -23.5, -46.6, null, agora);
        db.Relatos.Add(relato);
        await db.SaveChangesAsync();

        // Mesma ordem que o serviço vai usar: primeiro o INSERT do apoio (o gatilho soma +1 no banco),
        // depois o UPDATE do relato com o valor absoluto. Resultado: 1, e não 2.
        db.Apoios.Add(new ApoioRelato(relato.Id, bruno.Id, agora));
        await db.SaveChangesAsync();
        relato.RegistrarApoio(agora);
        var anterior = relato.AlterarStatus(StatusRelato.EM_TRIAGEM, null, null, agora);
        db.Atualizacoes.Add(new AtualizacaoRelato(relato.Id, marina.Id, anterior, relato.Status, "teste", agora));
        db.Idempotencias.Add(new RegistroIdempotencia(ana.Id, "teste-oracle", new string('a', 64), 201,
            "{\"mensagem\":\"Relato criado. Ação concluída.\"}", agora));
        await db.SaveChangesAsync();

        var totalNoBanco = await db.Relatos.Where(r => r.Id == relato.Id).Select(r => r.TotalApoios).SingleAsync();
        Assert.Equal(1, totalNoBanco);
        db.ChangeTracker.Clear();
        var relida = await db.Descricoes.SingleAsync(d => d.Id == descricao.Id);
        Assert.Equal(5000, relida.DescricaoCompleta!.Length);
        Assert.Equal("degrau", relida.Perigos.Single().Descricao);
        Assert.Contains("Ação", (await db.Idempotencias.SingleAsync(i => i.Chave == "teste-oracle")).CorpoResposta);

        await transacao.RollbackAsync();
    }
}

/// <summary>[Fact] que só roda quando a variável VEAI_ORACLE_TESTE está definida.</summary>
public sealed class OracleFactAttribute : FactAttribute
{
    public static string ConnectionString => Environment.GetEnvironmentVariable("VEAI_ORACLE_TESTE") ?? "";

    public OracleFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            Skip = "Defina VEAI_ORACLE_TESTE com a connection string de um Oracle com o schema do Vê AI.";
    }
}
