using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oracle.EntityFrameworkCore.Infrastructure;
using VeAI.Infrastructure.Persistence;

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

        services.AddHealthChecks()
            .AddDbContextCheck<VeAiDbContext>("oracle", tags: ["ready", "banco"],
                customTestQuery: (db, ct) => db.Regioes.AnyAsync(ct));

        return services;
    }
}
