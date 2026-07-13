using Kubix.Application.Tenancy;
using Kubix.Domain;
using Kubix.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Kubix.Infrastructure.Tenancy;

public static class ExtensionesServiciosTenancy
{
    public static IServiceCollection AgregarTenancy(this IServiceCollection services)
    {
        services.AddScoped<IContextoInquilino, ContextoInquilino>();
        services.AddScoped<IEscritorAuditoria, EscritorAuditoria>();

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                NombresPoliticas.SoloSuperAdmin,
                p => p.RequireRole(ConversorEnumDominio.ACadenaDb(RolUsuario.SuperAdministrador)));

            options.AddPolicy(
                NombresPoliticas.SoloCoordinador,
                p => p.RequireRole(ConversorEnumDominio.ACadenaDb(RolUsuario.Coordinador)));

            options.AddPolicy(
                NombresPoliticas.SoloConductor,
                p => p.RequireRole(ConversorEnumDominio.ACadenaDb(RolUsuario.Conductor)));

            options.AddPolicy(
                NombresPoliticas.SoloPasajero,
                p => p.RequireRole(ConversorEnumDominio.ACadenaDb(RolUsuario.Pasajero)));

            options.AddPolicy(
                NombresPoliticas.UsuarioMobile,
                p => p.RequireRole(
                    ConversorEnumDominio.ACadenaDb(RolUsuario.Conductor),
                    ConversorEnumDominio.ACadenaDb(RolUsuario.Pasajero)));
        });

        return services;
    }
}
