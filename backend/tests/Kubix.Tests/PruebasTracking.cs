using Kubix.Application.Tracking;
using Kubix.Domain.Entities;
using Kubix.Domain.Enums;
using Kubix.Infrastructure.Persistence;
using Kubix.Infrastructure.Seeding;
using Kubix.Infrastructure.Tenancy;
using Kubix.Infrastructure.Tracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kubix.Tests;

public class PruebasTracking
{
    [Fact]
    public async Task Pasajero_no_ve_pings_de_otros_pasajeros()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver2@utn.local");
        var pax1 = await db.Usuarios.SingleAsync(u => u.Correo == "pax1@utn.local");
        var pax2 = await db.Usuarios.SingleAsync(u => u.Correo == "pax2@utn.local");
        var viaje = await db.Viajes.SingleAsync(v =>
            v.Estado == EstadoViaje.EnCurso && v.UniversidadId == conductor.UniversidadId);

        db.SolicitudesViaje.Add(new SolicitudViaje
        {
            UniversidadId = viaje.UniversidadId,
            ViajeId = viaje.Id,
            PasajeroId = pax2.Id,
            RecogidaTexto = "Otro punto",
            RecogidaLat = -0.361,
            RecogidaLng = -78.141,
            Estado = EstadoSolicitudViaje.Aceptada
        });
        db.PingsUbicacion.Add(new PingUbicacion
        {
            UniversidadId = viaje.UniversidadId,
            ViajeId = viaje.Id,
            UsuarioId = pax2.Id,
            Lat = -0.362,
            Lng = -78.142,
            RegistradoEn = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var tracking = await CrearTracking(db, pax1).ObtenerTrackingAsync(pax1.Id, viaje.Id);

        Assert.Equal(viaje.Id, tracking.ViajeId);
        Assert.Contains(tracking.Participantes, p => p.UsuarioId == conductor.Id && p.Rol == "driver");
        Assert.Contains(tracking.Participantes, p => p.UsuarioId == pax1.Id && p.Rol == "passenger");
        Assert.DoesNotContain(tracking.Participantes, p => p.UsuarioId == pax2.Id);
    }

    [Fact]
    public async Task Conductor_ve_pickup_si_pasajero_sin_ping()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver2@utn.local");
        var pax1 = await db.Usuarios.SingleAsync(u => u.Correo == "pax1@utn.local");
        var pax2 = await db.Usuarios.SingleAsync(u => u.Correo == "pax2@utn.local");
        var viaje = await db.Viajes.SingleAsync(v =>
            v.Estado == EstadoViaje.EnCurso && v.UniversidadId == conductor.UniversidadId);

        // Quitar ping de pax1 (seed) y añadir pax2 aceptado sin ping
        var pingsPax1 = await db.PingsUbicacion
            .Where(p => p.ViajeId == viaje.Id && p.UsuarioId == pax1.Id)
            .ToListAsync();
        db.PingsUbicacion.RemoveRange(pingsPax1);

        var solicitud = new SolicitudViaje
        {
            UniversidadId = viaje.UniversidadId,
            ViajeId = viaje.Id,
            PasajeroId = pax2.Id,
            RecogidaTexto = "Pickup pax2",
            RecogidaLat = -0.37,
            RecogidaLng = -78.15,
            Estado = EstadoSolicitudViaje.Aceptada
        };
        db.SolicitudesViaje.Add(solicitud);
        await db.SaveChangesAsync();

        var tracking = await CrearTracking(db, conductor).ObtenerTrackingAsync(conductor.Id, viaje.Id);

        var pax1Item = Assert.Single(tracking.Participantes, p => p.UsuarioId == pax1.Id);
        Assert.Equal("boarding_point", pax1Item.Rol);
        Assert.Equal("boarding", pax1Item.Fuente);
        Assert.Equal(-0.358, pax1Item.Lat);

        var pax2Item = Assert.Single(tracking.Participantes, p => p.UsuarioId == pax2.Id);
        Assert.Equal("boarding_point", pax2Item.Rol);
        Assert.Equal("boarding", pax2Item.Fuente);
        Assert.Equal(-0.37, pax2Item.Lat);

        Assert.Contains(tracking.Participantes, p => p.UsuarioId == conductor.Id && p.Fuente == "ping");
    }

    [Fact]
    public async Task Conductor_ve_ping_y_abordaje_del_mismo_pasajero()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver2@utn.local");
        var pax1 = await db.Usuarios.SingleAsync(u => u.Correo == "pax1@utn.local");
        var viaje = await db.Viajes.SingleAsync(v =>
            v.Estado == EstadoViaje.EnCurso && v.UniversidadId == conductor.UniversidadId);

        var tracking = await CrearTracking(db, conductor).ObtenerTrackingAsync(conductor.Id, viaje.Id);

        Assert.Contains(
            tracking.Participantes,
            p => p.UsuarioId == pax1.Id && p.Rol == "passenger" && p.Fuente == "ping");
        Assert.Contains(
            tracking.Participantes,
            p => p.UsuarioId == pax1.Id && p.Rol == "boarding_point" && p.Fuente == "boarding");
    }

    [Fact]
    public async Task Ping_en_viaje_no_activo_devuelve_409()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver1@utn.local");
        var viaje = await db.Viajes.FirstAsync(v =>
            v.ConductorId == conductor.Id && v.Estado == EstadoViaje.Programado);
        viaje.Estado = EstadoViaje.Completado;
        viaje.CompletadoEn = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ExcepcionTracking>(() =>
            CrearTracking(db, conductor).RegistrarPingAsync(
                conductor.Id,
                viaje.Id,
                new SolicitudPing { Lat = -0.35, Lng = -78.12 }));

        Assert.Equal(409, ex.CodigoEstado);
        Assert.Equal("trip_not_active", ex.Codigo);
    }

    [Fact]
    public async Task Ping_de_no_participante_devuelve_403()
    {
        await using var db = await CrearDbConSeedAsync();
        var ajeno = await db.Usuarios.SingleAsync(u => u.Correo == "pax2@utn.local");
        var viaje = await db.Viajes.FirstAsync(v =>
            v.UniversidadId == ajeno.UniversidadId && v.Estado == EstadoViaje.EnCurso);

        var ex = await Assert.ThrowsAsync<ExcepcionTracking>(() =>
            CrearTracking(db, ajeno).RegistrarPingAsync(
                ajeno.Id,
                viaje.Id,
                new SolicitudPing { Lat = -0.35, Lng = -78.12 }));

        Assert.Equal(403, ex.CodigoEstado);
        Assert.Equal("not_participant", ex.Codigo);
    }

    [Fact]
    public async Task Admin_ve_todos_los_participantes_de_viajes_activos()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver2@utn.local");
        var pax1 = await db.Usuarios.SingleAsync(u => u.Correo == "pax1@utn.local");
        var pax2 = await db.Usuarios.SingleAsync(u => u.Correo == "pax2@utn.local");
        var coord = await db.Usuarios.SingleAsync(u => u.Correo == "coordinador@utn.local");
        var viaje = await db.Viajes.SingleAsync(v =>
            v.Estado == EstadoViaje.EnCurso && v.UniversidadId == conductor.UniversidadId);

        db.SolicitudesViaje.Add(new SolicitudViaje
        {
            UniversidadId = viaje.UniversidadId,
            ViajeId = viaje.Id,
            PasajeroId = pax2.Id,
            RecogidaTexto = "Extra",
            RecogidaLat = -0.361,
            RecogidaLng = -78.141,
            Estado = EstadoSolicitudViaje.Aceptada
        });
        db.PingsUbicacion.Add(new PingUbicacion
        {
            UniversidadId = viaje.UniversidadId,
            ViajeId = viaje.Id,
            UsuarioId = pax2.Id,
            Lat = -0.363,
            Lng = -78.143,
            RegistradoEn = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var activos = await CrearTracking(db, coord).ListarActivosAdminAsync();
        var item = Assert.Single(activos.Viajes, t => t.ViajeId == viaje.Id);

        Assert.Contains(item.Participantes, p => p.UsuarioId == conductor.Id);
        Assert.Contains(item.Participantes, p => p.UsuarioId == pax1.Id);
        Assert.Contains(item.Participantes, p => p.UsuarioId == pax2.Id);
    }

    [Fact]
    public async Task Admin_incluye_conductor_con_nombre_sin_ping()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver2@utn.local");
        var coord = await db.Usuarios.SingleAsync(u => u.Correo == "coordinador@utn.local");
        var viaje = await db.Viajes.SingleAsync(v =>
            v.Estado == EstadoViaje.EnCurso && v.UniversidadId == conductor.UniversidadId);

        var pingsConductor = await db.PingsUbicacion
            .Where(p => p.ViajeId == viaje.Id && p.UsuarioId == conductor.Id)
            .ToListAsync();
        db.PingsUbicacion.RemoveRange(pingsConductor);
        await db.SaveChangesAsync();

        var activos = await CrearTracking(db, coord).ListarActivosAdminAsync();
        var item = Assert.Single(activos.Viajes, t => t.ViajeId == viaje.Id);
        var driver = Assert.Single(item.Participantes, p => p.Rol == "driver");

        Assert.Equal(conductor.Id, driver.UsuarioId);
        Assert.False(string.IsNullOrWhiteSpace(driver.Nombre));
        Assert.Equal(conductor.Nombre, driver.Nombre);
        Assert.Equal("origin", driver.Fuente);
    }

    [Fact]
    public async Task Coordinador_en_tracking_mobile_devuelve_403()
    {
        await using var db = await CrearDbConSeedAsync();
        var coord = await db.Usuarios.SingleAsync(u => u.Correo == "coordinador@utn.local");
        var viaje = await db.Viajes.FirstAsync(v =>
            v.UniversidadId == coord.UniversidadId && v.Estado == EstadoViaje.EnCurso);

        var ex = await Assert.ThrowsAsync<ExcepcionTracking>(() =>
            CrearTracking(db, coord).ObtenerTrackingAsync(coord.Id, viaje.Id));

        Assert.Equal(403, ex.CodigoEstado);
        Assert.Equal("use_admin_tracking", ex.Codigo);
    }

    [Fact]
    public async Task Retencion_borra_viejos_y_conserva_ultimo_por_usuario()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver1@utn.local");
        var pax = await db.Usuarios.SingleAsync(u => u.Correo == "pax1@utn.local");
        var campus = await db.Sedes.FirstAsync(c => c.Id == conductor.CampusId);

        var terminadoEn = DateTimeOffset.UtcNow.AddDays(-10);
        var viaje = new Viaje
        {
            UniversidadId = conductor.UniversidadId!.Value,
            ConductorId = conductor.Id,
            CampusDestinoId = campus.Id,
            OrigenTexto = "Retención test",
            OrigenLat = -0.34,
            OrigenLng = -78.12,
            SaleEn = terminadoEn.AddHours(-1),
            AsientosDisponibles = 0,
            DistanciaKm = 5m,
            Estado = EstadoViaje.Completado,
            IniciadoEn = terminadoEn.AddMinutes(-40),
            CompletadoEn = terminadoEn,
            CreadoEn = terminadoEn.AddHours(-2),
            ActualizadoEn = terminadoEn
        };
        db.Viajes.Add(viaje);
        await db.SaveChangesAsync();

        var viejoConductor = new PingUbicacion
        {
            UniversidadId = viaje.UniversidadId,
            ViajeId = viaje.Id,
            UsuarioId = conductor.Id,
            Lat = 1,
            Lng = 1,
            RegistradoEn = terminadoEn.AddMinutes(-30)
        };
        var ultimoConductor = new PingUbicacion
        {
            UniversidadId = viaje.UniversidadId,
            ViajeId = viaje.Id,
            UsuarioId = conductor.Id,
            Lat = 2,
            Lng = 2,
            RegistradoEn = terminadoEn.AddMinutes(-5)
        };
        var viejoPax = new PingUbicacion
        {
            UniversidadId = viaje.UniversidadId,
            ViajeId = viaje.Id,
            UsuarioId = pax.Id,
            Lat = 3,
            Lng = 3,
            RegistradoEn = terminadoEn.AddMinutes(-20)
        };
        var ultimoPax = new PingUbicacion
        {
            UniversidadId = viaje.UniversidadId,
            ViajeId = viaje.Id,
            UsuarioId = pax.Id,
            Lat = 4,
            Lng = 4,
            RegistradoEn = terminadoEn.AddMinutes(-1)
        };
        db.PingsUbicacion.AddRange(viejoConductor, ultimoConductor, viejoPax, ultimoPax);
        await db.SaveChangesAsync();

        var retencion = new ServicioRetencionPings(db);
        var borrados = await retencion.EjecutarRetencionAsync();
        Assert.Equal(2, borrados);

        var restantes = await db.PingsUbicacion
            .Where(p => p.ViajeId == viaje.Id)
            .OrderBy(p => p.UsuarioId)
            .ToListAsync();
        Assert.Equal(2, restantes.Count);
        Assert.Contains(restantes, p => p.Id == ultimoConductor.Id && p.Lat == 2);
        Assert.Contains(restantes, p => p.Id == ultimoPax.Id && p.Lat == 4);
        Assert.DoesNotContain(restantes, p => p.Id == viejoConductor.Id);
        Assert.DoesNotContain(restantes, p => p.Id == viejoPax.Id);
    }

    [Fact]
    public async Task Retencion_no_toca_viajes_terminales_recientes()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver1@utn.local");
        var campus = await db.Sedes.FirstAsync(c => c.Id == conductor.CampusId);
        var terminadoEn = DateTimeOffset.UtcNow.AddDays(-2);

        var viaje = new Viaje
        {
            UniversidadId = conductor.UniversidadId!.Value,
            ConductorId = conductor.Id,
            CampusDestinoId = campus.Id,
            OrigenTexto = "Reciente",
            OrigenLat = -0.34,
            OrigenLng = -78.12,
            SaleEn = terminadoEn.AddHours(-1),
            AsientosDisponibles = 1,
            DistanciaKm = 3m,
            Estado = EstadoViaje.Completado,
            CompletadoEn = terminadoEn,
            ActualizadoEn = terminadoEn
        };
        db.Viajes.Add(viaje);
        await db.SaveChangesAsync();

        var ping1 = new PingUbicacion
        {
            UniversidadId = viaje.UniversidadId,
            ViajeId = viaje.Id,
            UsuarioId = conductor.Id,
            Lat = 1,
            Lng = 1,
            RegistradoEn = terminadoEn.AddMinutes(-10)
        };
        var ping2 = new PingUbicacion
        {
            UniversidadId = viaje.UniversidadId,
            ViajeId = viaje.Id,
            UsuarioId = conductor.Id,
            Lat = 2,
            Lng = 2,
            RegistradoEn = terminadoEn.AddMinutes(-1)
        };
        db.PingsUbicacion.AddRange(ping1, ping2);
        await db.SaveChangesAsync();

        var borrados = await new ServicioRetencionPings(db).EjecutarRetencionAsync();
        Assert.Equal(0, borrados);
        Assert.Equal(2, await db.PingsUbicacion.CountAsync(p => p.ViajeId == viaje.Id));
    }

    [Fact]
    public async Task Participante_puede_registrar_ping()
    {
        await using var db = await CrearDbConSeedAsync();
        var pax = await db.Usuarios.SingleAsync(u => u.Correo == "pax1@utn.local");
        var viaje = await db.Viajes.FirstAsync(v =>
            v.UniversidadId == pax.UniversidadId && v.Estado == EstadoViaje.EnCurso);

        var ping = await CrearTracking(db, pax).RegistrarPingAsync(
            pax.Id,
            viaje.Id,
            new SolicitudPing { Lat = -0.3595, Lng = -78.1385 });

        Assert.Equal(pax.Id, ping.UsuarioId);
        Assert.Equal(viaje.Id, ping.ViajeId);
        Assert.Equal(-0.3595, ping.Lat);
        Assert.True(await db.PingsUbicacion.AnyAsync(p => p.Id == ping.Id));
    }

    private static IServicioTracking CrearTracking(ContextoApp db, Usuario usuario)
    {
        if (db is not ContextoAppConInquilino mutable)
        {
            throw new InvalidOperationException("Se espera ContextoAppConInquilino.");
        }

        mutable.Inquilino.OmitirFiltros = false;
        mutable.Inquilino.UniversidadId = usuario.UniversidadId;
        mutable.Inquilino.CampusId = usuario.CampusId;
        mutable.Inquilino.UsuarioId = usuario.Id;
        mutable.Inquilino.Rol = usuario.Rol;
        return new ServicioTracking(db, mutable.Inquilino);
    }

    private static async Task<ContextoApp> CrearDbConSeedAsync()
    {
        var inquilino = new ContextoInquilino { OmitirFiltros = true };
        var db = new ContextoAppConInquilino(CrearOpciones(), inquilino);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SUPER_ADMIN_EMAIL"] = "superadmin@kubix.local",
                ["SUPER_ADMIN_PASSWORD"] = SembradorBaseDatos.ContrasenaPorDefecto
            })
            .Build();

        var sembrador = new SembradorBaseDatos(db, config, NullLogger<SembradorBaseDatos>.Instance);
        await sembrador.SembrarDemoAsync();
        return db;
    }

    private static DbContextOptions<ContextoApp> CrearOpciones() =>
        new DbContextOptionsBuilder<ContextoApp>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private sealed class ContextoAppConInquilino(
        DbContextOptions<ContextoApp> options,
        ContextoInquilino inquilino) : ContextoApp(options, inquilino)
    {
        public ContextoInquilino Inquilino { get; } = inquilino;
    }
}
