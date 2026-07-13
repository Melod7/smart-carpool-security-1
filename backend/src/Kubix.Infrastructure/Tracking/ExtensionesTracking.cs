using Kubix.Application.Tracking;
using Microsoft.Extensions.DependencyInjection;

namespace Kubix.Infrastructure.Tracking;

public static class ExtensionesTracking
{
    public static IServiceCollection AgregarTracking(this IServiceCollection services)
    {
        services.AddScoped<IServicioTracking, ServicioTracking>();
        services.AddScoped<IServicioRetencionPings, ServicioRetencionPings>();
        services.AddHostedService<TrabajoRetencionPings>();
        return services;
    }
}
