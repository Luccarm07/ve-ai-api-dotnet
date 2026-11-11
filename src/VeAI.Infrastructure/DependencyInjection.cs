using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oracle.EntityFrameworkCore.Infrastructure;
using VeAI.Application.Abstracoes;
using VeAI.Infrastructure.Manutencao;
using VeAI.Infrastructure.Mensageria;
using VeAI.Infrastructure.Persistence;
using VeAI.Infrastructure.Persistence.Repositorios;
using VeAI.Infrastructure.Seguranca;

namespace VeAI.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Oracle";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddDbContext<VeAiDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException(
                    "Banco não configurado: defina ConnectionStrings__Oracle no arquivo .env (veja .env.example).");

            // Compatibilidade 19c (Oracle da FIAP) e 21c (XE do Docker): booleanos em NUMBER(1), sem tipo BOOLEAN.
            options.UseOracle(connectionString, o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19));
        });

        services.AddScoped<IUsuarioRepositorio, UsuarioRepositorio>();
        services.AddScoped<IRegiaoRepositorio, RegiaoRepositorio>();
        services.AddScoped<IRelatoRepositorio, RelatoRepositorio>();
        services.AddScoped<IApoioRepositorio, ApoioRepositorio>();
        services.AddScoped<IAtualizacaoRepositorio, AtualizacaoRepositorio>();
        services.AddScoped<IDescricaoRepositorio, DescricaoRepositorio>();
        services.AddScoped<IIdempotenciaRepositorio, IdempotenciaRepositorio>();
        services.AddScoped<IUnidadeDeTrabalho, UnidadeDeTrabalho>();
        services.AddSingleton<IPublicadorEventos, PublicadorEventosLog>();

        // Recalcular prioridade e limpar idempotência vencida (desligável: Manutencao__Ativa=false).
        if (configuration.GetValue("Manutencao:Ativa", true))
            services.AddHostedService<ManutencaoPeriodica>();
        services.AddSingleton<ISenhaHasher, SenhaHasherBCrypt>();
        services.AddSingleton<IGeradorToken, GeradorTokenJwt>();

        services.AddHealthChecks()
            .AddDbContextCheck<VeAiDbContext>("oracle", tags: ["ready", "banco"],
                customTestQuery: (db, ct) => db.Regioes.AnyAsync(ct));

        return services;
    }
}
