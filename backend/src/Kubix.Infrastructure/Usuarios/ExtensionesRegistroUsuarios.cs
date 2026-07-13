using Kubix.Application.Usuarios;
using Microsoft.Extensions.DependencyInjection;

namespace Kubix.Infrastructure.Usuarios;

public static class ExtensionesRegistroUsuarios
{
    public static IServiceCollection AgregarRegistroUsuarios(this IServiceCollection services)
    {
        services.AddScoped<IServicioRegistroUsuarios, ServicioRegistroUsuarios>();
        return services;
    }
}
