using Kubix.Application.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kubix.Infrastructure.Auth;

public static class ExtensionesServiciosAuth
{
    public static IServiceCollection AgregarServiciosAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<OpcionesJwt>(configuration.GetSection(OpcionesJwt.Seccion));
        services.AddMemoryCache();
        services.AddScoped<ICacheEstadoUsuario, CacheEstadoUsuario>();
        services.AddScoped<IServicioTokenJwt, ServicioTokenJwt>();
        services.AddScoped<IServicioAutenticacion, ServicioAutenticacion>();
        return services;
    }
}
