using Kubix.Domain;
using Kubix.Domain.Enums;
using Kubix.Infrastructure.Persistence;
using Kubix.Infrastructure.Seeding;
using Kubix.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kubix.Tests;

public class PruebasEsquemaYSeed
{
    [Fact]
    public void Enums_convierten_a_valores_snake_case_ingles_en_db()
    {
        Assert.Equal("super_admin", ConversorEnumDominio.ACadenaDb(RolUsuario.SuperAdministrador));
        Assert.Equal("in_progress", ConversorEnumDominio.ACadenaDb(EstadoViaje.EnCurso));
        Assert.Equal("cancelled_by_passenger", ConversorEnumDominio.ACadenaDb(EstadoSolicitudViaje.CanceladaPorPasajero));
        Assert.Equal("coordinador", ConversorEnumDominio.ACadenaDb(RolUsuario.Coordinador));
        Assert.Equal("driver", ConversorEnumDominio.ACadenaDb(RolUsuario.Conductor));
        Assert.Equal(RolUsuario.Coordinador, ConversorEnumDominio.DesdeCadenaDb<RolUsuario>("coordinador"));
        Assert.Equal(TipoTransaccionEcoToken.ViajeCompletadoConductor,
            ConversorEnumDominio.DesdeCadenaDb<TipoTransaccionEcoToken>("trip_completed_driver"));
    }

    [Fact]
    public async Task Seed_es_idempotente_y_llena_university_id_en_tablas_hijas()
    {
        await using var db = CrearDb();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SUPER_ADMIN_EMAIL"] = "superadmin@kubix.local",
                ["SUPER_ADMIN_PASSWORD"] = SembradorBaseDatos.ContrasenaPorDefecto
            })
            .Build();

        var sembrador = new SembradorBaseDatos(db, config, NullLogger<SembradorBaseDatos>.Instance);
        await sembrador.SembrarAsync();
        await sembrador.SembrarAsync();

        Assert.Equal(1, await db.Usuarios.CountAsync(u => u.Rol == RolUsuario.SuperAdministrador));
        Assert.Equal(2, await db.Universidades.CountAsync());
        Assert.Equal(4, await db.Sedes.CountAsync());
        Assert.True(await db.Usuarios.CountAsync() >= 15);

        Assert.All(await db.Vehiculos.ToListAsync(), v => Assert.NotEqual(Guid.Empty, v.UniversidadId));
        Assert.All(await db.SolicitudesViaje.ToListAsync(), r => Assert.NotEqual(Guid.Empty, r.UniversidadId));
        Assert.All(await db.Calificaciones.ToListAsync(), r => Assert.NotEqual(Guid.Empty, r.UniversidadId));
        Assert.All(await db.ContactosEmergencia.ToListAsync(), c => Assert.NotEqual(Guid.Empty, c.UniversidadId));
        Assert.All(await db.PingsUbicacion.ToListAsync(), p => Assert.NotEqual(Guid.Empty, p.UniversidadId));
        Assert.All(await db.TransaccionesEcoToken.ToListAsync(), t => Assert.NotEqual(Guid.Empty, t.UniversidadId));

        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver1@utn.local");
        Assert.True(BCrypt.Net.BCrypt.Verify(SembradorBaseDatos.ContrasenaPorDefecto, conductor.HashContrasena));

        var paresUnicos = await db.TransaccionesEcoToken
            .Select(t => new { t.UsuarioId, t.Tipo, t.IdFuente })
            .Distinct()
            .CountAsync();
        Assert.Equal(await db.TransaccionesEcoToken.CountAsync(), paresUnicos);
    }

    private static ContextoApp CrearDb()
    {
        var opciones = new DbContextOptionsBuilder<ContextoApp>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ContextoApp(opciones, new ContextoInquilino { OmitirFiltros = true });
    }
}
