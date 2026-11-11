using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oracle.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using VeAI.Application.Abstracoes;
using VeAI.Application.Opcoes;
using VeAI.Infrastructure.Ia;
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

        // IA: descritor falso (padrão, sem custo) ou OpenAI de verdade (Ia__Provedor=openai + OPENAI_API_KEY no .env).
        services.Configure<IaOptions>(configuration.GetSection(IaOptions.Secao));
        services.PostConfigure<IaOptions>(o => o.ApiKey ??= configuration["OPENAI_API_KEY"]);
        var ia = configuration.GetSection(IaOptions.Secao).Get<IaOptions>() ?? new IaOptions();
        if (ia.OpenAi)
            services.AddHttpClient<IDescritorImagem, DescritorOpenAi>(c =>
            {
                c.BaseAddress = new Uri(ia.BaseUrl);
                c.Timeout = TimeSpan.FromSeconds(ia.TimeoutSegundos);
            });
        else
            services.AddSingleton<IDescritorImagem, DescritorFalso>();
        services.AddHttpClient(IaHealthCheck.ClienteHttp, c =>
        {
            c.BaseAddress = new Uri(ia.BaseUrl);
            c.Timeout = TimeSpan.FromSeconds(3);
        });

        services.AddSingleton<IaHealthCheck>(); // singleton: o cache de 60 s vale entre chamadas do /health

        services.AddHealthChecks()
            .AddCheck<IaHealthCheck>("ia", failureStatus: HealthStatus.Degraded, tags: ["ready", "externo"])
            .AddDbContextCheck<VeAiDbContext>("oracle", tags: ["ready", "banco"],
                customTestQuery: (db, ct) => db.Regioes.AnyAsync(ct));

        return services;
    }
}
