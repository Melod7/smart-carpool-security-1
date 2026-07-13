using Kubix.Application.Admin;
using Microsoft.Extensions.DependencyInjection;

namespace Kubix.Infrastructure.Admin;

public static class ExtensionesAdminOps
{
    public static IServiceCollection AgregarAdminOps(this IServiceCollection services)
    {
        services.AddScoped<IServicioAdminOps, ServicioAdminOps>();
        return services;
    }
}
