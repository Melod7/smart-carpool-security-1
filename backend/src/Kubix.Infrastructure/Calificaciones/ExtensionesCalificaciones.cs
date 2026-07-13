using Kubix.Application.Calificaciones;
using Microsoft.Extensions.DependencyInjection;

namespace Kubix.Infrastructure.Calificaciones;

public static class ExtensionesCalificaciones
{
    public static IServiceCollection AgregarCalificaciones(this IServiceCollection services)
    {
        services.AddScoped<IServicioCalificaciones, ServicioCalificaciones>();
        return services;
    }
}
