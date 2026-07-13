using Kubix.Application.EcoTokens;
using Microsoft.Extensions.DependencyInjection;

namespace Kubix.Infrastructure.EcoTokens;

public static class ExtensionesEcoTokens
{
    public static IServiceCollection AgregarEcoTokens(this IServiceCollection services)
    {
        services.AddScoped<MotorEcoTokens>();
        services.AddScoped<IMotorEcoTokens>(sp => sp.GetRequiredService<MotorEcoTokens>());
        services.AddScoped<IServicioEcoTokens>(sp => sp.GetRequiredService<MotorEcoTokens>());
        return services;
    }
}
