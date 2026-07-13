using Kubix.Application.EcoTokens;
using Kubix.Application.Viajes;
using Kubix.Infrastructure.EcoTokens;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kubix.Infrastructure.Viajes;

public static class ExtensionesViajes
{
    public static IServiceCollection AgregarViajes(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<OpcionesGoogleMaps>(options =>
        {
            configuration.GetSection(OpcionesGoogleMaps.Seccion).Bind(options);
            if (string.IsNullOrWhiteSpace(options.ApiKey))
            {
                options.ApiKey = configuration["GOOGLE_MAPS_API_KEY"];
            }
        });

        services.AddHttpClient<IServicioDirections, ServicioDirectionsGoogle>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        services.AddScoped<IMotorEcoTokens, MotorEcoTokensStub>();
        services.AddScoped<IServicioViajes, ServicioViajes>();
        services.AddScoped<IServicioSolicitudesViaje, ServicioSolicitudesViaje>();
        return services;
    }
}
