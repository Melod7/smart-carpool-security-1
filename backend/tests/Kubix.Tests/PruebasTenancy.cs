using System.Security.Claims;
using Kubix.Application.Tenancy;
using Kubix.Domain.Entities;
using Kubix.Domain.Enums;
using Kubix.Infrastructure.Persistence;
using Kubix.Infrastructure.Seeding;
using Kubix.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kubix.Tests;

public class PruebasTenancy
{
    [Fact]
    public async Task Filtro_inquilino_solo_ve_datos_de_su_universidad()
    {
        var uniA = Guid.NewGuid();
        var uniB = Guid.NewGuid();
        var inquilino = new ContextoInquilino { OmitirFiltros = true };
        await using var db = CrearDb(inquilino);

        await SembrarDosUniversidadesAsync(db, uniA, uniB);

        inquilino.OmitirFiltros = false;
        inquilino.UniversidadId = uniA;

        var vehiculos = await db.Vehiculos.ToListAsync();
        var usuarios = await db.Usuarios.Where(u => u.UniversidadId != null).ToListAsync();

        Assert.All(vehiculos, v => Assert.Equal(uniA, v.UniversidadId));
        Assert.All(usuarios, u => Assert.Equal(uniA, u.UniversidadId));
        Assert.DoesNotContain(vehiculos, v => v.UniversidadId == uniB);
        Assert.True(vehiculos.Count >= 1);
        Assert.True(usuarios.Count >= 1);
    }

    [Fact]
    public async Task Super_admin_con_OmitirFiltros_ve_ambas_universidades()
    {
        var uniA = Guid.NewGuid();
        var uniB = Guid.NewGuid();
        var inquilino = new ContextoInquilino
        {
            OmitirFiltros = true,
            Rol = RolUsuario.SuperAdministrador
        };
        await using var db = CrearDb(inquilino);

        await SembrarDosUniversidadesAsync(db, uniA, uniB);

        var vehiculos = await db.Vehiculos.ToListAsync();
        var universidades = await db.Universidades.ToListAsync();

        Assert.Contains(vehiculos, v => v.UniversidadId == uniA);
        Assert.Contains(vehiculos, v => v.UniversidadId == uniB);
        Assert.Equal(2, universidades.Count);
    }

    [Fact]
    public async Task SaveChanges_estampa_UniversidadId_en_Vehiculo_y_ContactoEmergencia()
    {
        var uniA = Guid.NewGuid();
        var uniB = Guid.NewGuid();
        var inquilino = new ContextoInquilino { OmitirFiltros = true };
        await using var db = CrearDb(inquilino);

        await SembrarDosUniversidadesAsync(db, uniA, uniB);

        var campusA = await db.Sedes.FirstAsync(c => c.UniversidadId == uniA);
        var ahora = DateTimeOffset.UtcNow;
        var nuevoConductor = new Usuario
        {
            Id = Guid.NewGuid(),
            UniversidadId = uniA,
            CampusId = campusA.Id,
            Rol = RolUsuario.Conductor,
            Estado = EstadoUsuario.Activo,
            Nombre = "Nuevo Conductor",
            Correo = "nuevo-driver@a.local",
            HashContrasena = BCrypt.Net.BCrypt.HashPassword(SembradorBaseDatos.ContrasenaPorDefecto),
            CreadoEn = ahora,
            ActualizadoEn = ahora
        };
        db.Usuarios.Add(nuevoConductor);
        await db.SaveChangesAsync();

        inquilino.OmitirFiltros = false;
        inquilino.UniversidadId = uniA;
        inquilino.UsuarioId = nuevoConductor.Id;
        inquilino.Rol = RolUsuario.Conductor;

        var vehiculo = new Vehiculo
        {
            UniversidadId = Guid.Empty,
            UsuarioId = nuevoConductor.Id,
            MarcaModelo = "Toyota Corolla",
            Placa = "ABC-999",
            Color = "Rojo",
            AsientosTotales = 4
        };
        db.Vehiculos.Add(vehiculo);

        var contacto = new ContactoEmergencia
        {
            UniversidadId = Guid.Empty,
            UsuarioId = nuevoConductor.Id,
            Nombre = "Contacto Test",
            Relacion = "Hermano",
            Telefono = "+593999000111"
        };
        db.ContactosEmergencia.Add(contacto);

        await db.SaveChangesAsync();

        Assert.Equal(uniA, vehiculo.UniversidadId);
        Assert.Equal(uniA, contacto.UniversidadId);
    }

    [Fact]
    public async Task Politicas_autorizacion_respetan_roles()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AgregarTenancy();
        await using var sp = services.BuildServiceProvider();

        var authz = sp.GetRequiredService<IAuthorizationService>();
        var conductor = CrearPrincipal("driver");

        var fallaCoordinador = await authz.AuthorizeAsync(conductor, resource: null, NombresPoliticas.SoloCoordinador);
        var pasaMobile = await authz.AuthorizeAsync(conductor, resource: null, NombresPoliticas.UsuarioMobile);
        var pasaConductor = await authz.AuthorizeAsync(conductor, resource: null, NombresPoliticas.SoloConductor);

        Assert.False(fallaCoordinador.Succeeded);
        Assert.True(pasaMobile.Succeeded);
        Assert.True(pasaConductor.Succeeded);
    }

    private static ClaimsPrincipal CrearPrincipal(string rol)
    {
        var identidad = new ClaimsIdentity(
            [new Claim("role", rol), new Claim(ClaimTypes.Role, rol)],
            authenticationType: "Test");
        return new ClaimsPrincipal(identidad);
    }

    private static async Task SembrarDosUniversidadesAsync(ContextoApp db, Guid uniA, Guid uniB)
    {
        var ahora = DateTimeOffset.UtcNow;
        var hash = BCrypt.Net.BCrypt.HashPassword(SembradorBaseDatos.ContrasenaPorDefecto);

        db.Universidades.AddRange(
            new Universidad { Id = uniA, Nombre = "Uni A", Slug = "uni-a", CreadoEn = ahora, ActualizadoEn = ahora },
            new Universidad { Id = uniB, Nombre = "Uni B", Slug = "uni-b", CreadoEn = ahora, ActualizadoEn = ahora });

        var campusA = new Campus
        {
            Id = Guid.NewGuid(),
            UniversidadId = uniA,
            Nombre = "Campus A",
            Direccion = "Dir A",
            Lat = 0,
            Lng = 0,
            CreadoEn = ahora,
            ActualizadoEn = ahora
        };
        var campusB = new Campus
        {
            Id = Guid.NewGuid(),
            UniversidadId = uniB,
            Nombre = "Campus B",
            Direccion = "Dir B",
            Lat = 1,
            Lng = 1,
            CreadoEn = ahora,
            ActualizadoEn = ahora
        };
        db.Sedes.AddRange(campusA, campusB);

        var driverA = new Usuario
        {
            Id = Guid.NewGuid(),
            UniversidadId = uniA,
            CampusId = campusA.Id,
            Rol = RolUsuario.Conductor,
            Estado = EstadoUsuario.Activo,
            Nombre = "Driver A",
            Correo = "driver@a.local",
            HashContrasena = hash,
            CreadoEn = ahora,
            ActualizadoEn = ahora
        };
        var driverB = new Usuario
        {
            Id = Guid.NewGuid(),
            UniversidadId = uniB,
            CampusId = campusB.Id,
            Rol = RolUsuario.Conductor,
            Estado = EstadoUsuario.Activo,
            Nombre = "Driver B",
            Correo = "driver@b.local",
            HashContrasena = hash,
            CreadoEn = ahora,
            ActualizadoEn = ahora
        };
        var paxA = new Usuario
        {
            Id = Guid.NewGuid(),
            UniversidadId = uniA,
            CampusId = campusA.Id,
            Rol = RolUsuario.Pasajero,
            Estado = EstadoUsuario.Activo,
            Nombre = "Pax A",
            Correo = "pax@a.local",
            HashContrasena = hash,
            CreadoEn = ahora,
            ActualizadoEn = ahora
        };
        var paxB = new Usuario
        {
            Id = Guid.NewGuid(),
            UniversidadId = uniB,
            CampusId = campusB.Id,
            Rol = RolUsuario.Pasajero,
            Estado = EstadoUsuario.Activo,
            Nombre = "Pax B",
            Correo = "pax@b.local",
            HashContrasena = hash,
            CreadoEn = ahora,
            ActualizadoEn = ahora
        };
        db.Usuarios.AddRange(driverA, driverB, paxA, paxB);

        db.Vehiculos.AddRange(
            new Vehiculo
            {
                Id = Guid.NewGuid(),
                UniversidadId = uniA,
                UsuarioId = driverA.Id,
                MarcaModelo = "Car A",
                Placa = "AAA-111",
                Color = "Azul",
                AsientosTotales = 4,
                CreadoEn = ahora,
                ActualizadoEn = ahora
            },
            new Vehiculo
            {
                Id = Guid.NewGuid(),
                UniversidadId = uniB,
                UsuarioId = driverB.Id,
                MarcaModelo = "Car B",
                Placa = "BBB-222",
                Color = "Verde",
                AsientosTotales = 4,
                CreadoEn = ahora,
                ActualizadoEn = ahora
            });

        await db.SaveChangesAsync();
    }

    private static ContextoApp CrearDb(IContextoInquilino inquilino)
    {
        var opciones = new DbContextOptionsBuilder<ContextoApp>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ContextoApp(opciones, inquilino);
    }
}
