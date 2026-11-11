using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VeAI.Domain.Entities;

namespace VeAI.Infrastructure.Persistence;

/// <summary>
/// Contexto do EF Core sobre o schema Oracle do Vê AI (scripts em database/oracle, os mesmos da API Java).
/// A API não cria nem altera tabelas: só mapeia as que os scripts criam.
/// </summary>
public class VeAiDbContext(DbContextOptions<VeAiDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Regiao> Regioes => Set<Regiao>();
    public DbSet<Descricao> Descricoes => Set<Descricao>();
    public DbSet<Relato> Relatos => Set<Relato>();
    public DbSet<ApoioRelato> Apoios => Set<ApoioRelato>();
    public DbSet<AtualizacaoRelato> Atualizacoes => Set<AtualizacaoRelato>();
    public DbSet<Notificacao> Notificacoes => Set<Notificacao>();
    public DbSet<RegistroIdempotencia> Idempotencias => Set<RegistroIdempotencia>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VeAiDbContext).Assembly);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Datas são gravadas em UTC (coluna DATE do Oracle não guarda fuso) e lidas de volta como UTC,
        // para o JSON sair com "Z", igual ao Instant da API Java.
        configurationBuilder.Properties<DateTime>().HaveConversion<DataUtcConverter>().HaveColumnType("DATE");
    }

    private sealed class DataUtcConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}
