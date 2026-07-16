using Kubix.Application.Usuarios;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kubix.Infrastructure.Usuarios;

public static class ExtensionesRegistroUsuarios
{
    public static IServiceCollection AgregarRegistroUsuarios(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<OpcionesEmail>(configuration.GetSection(OpcionesEmail.Seccion));
        services.AddScoped<IServicioCorreo, ServicioCorreoSmtp>();
        services.AddScoped<IServicioRegistroUsuarios, ServicioRegistroUsuarios>();
        return services;
    }
}
