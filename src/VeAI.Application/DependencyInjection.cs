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
        return services;
    }
}
