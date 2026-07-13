using Kubix.Application.SuperAdmin;
using Microsoft.Extensions.DependencyInjection;

namespace Kubix.Infrastructure.SuperAdmin;

public static class ExtensionesSuperAdmin
{
    public static IServiceCollection AgregarSuperAdmin(this IServiceCollection services)
    {
        services.AddScoped<IServicioSuperAdmin, ServicioSuperAdmin>();
        return services;
    }
}
