using Kubix.Application.Sos;
using Microsoft.Extensions.DependencyInjection;

namespace Kubix.Infrastructure.Sos;

public static class ExtensionesSos
{
    public static IServiceCollection AgregarSos(this IServiceCollection services)
    {
        services.AddScoped<IServicioSos, ServicioSos>();
        return services;
    }
}
