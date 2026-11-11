using Microsoft.Extensions.DependencyInjection;
using VeAI.Application.Servicos;

namespace VeAI.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthServico>();
        services.AddScoped<UsuarioServico>();
        services.AddScoped<RegiaoServico>();
        services.AddScoped<RelatoServico>();
        services.AddScoped<GestorRelatoServico>();
        services.AddScoped<RecalculoPrioridadeServico>();
        services.AddScoped<IdempotenciaServico>();
        services.AddScoped<DescricaoServico>();
        services.AddSingleton<MemoriaSugestoes>();
        services.AddScoped<NotificacaoServico>();
        return services;
    }
}
