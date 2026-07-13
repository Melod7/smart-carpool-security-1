using Kubix.Application.Calificaciones;
using Kubix.Application.Auth;
using Kubix.Domain.Entities;
using Kubix.Domain.Enums;
using Kubix.Infrastructure.Auth;
using Kubix.Infrastructure.Calificaciones;
using Kubix.Infrastructure.EcoTokens;
using Kubix.Infrastructure.Persistence;
using Kubix.Infrastructure.Seeding;
using Kubix.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kubix.Tests;

public class PruebasCalificaciones
{
    [Fact]
    public async Task Conductor_califica_fuera_de_ventana_12h_devuelve_422()
    {
        await using var db = await CrearDbConSeedAsync();
        var (viaje, conductor, pax) = await CrearViajeCompletadoAsync(
            db,
            completadoEn: DateTimeOffset.UtcNow.AddHours(-13));

        var servicio = CrearServicio(db, conductor);
        var ex = await Assert.ThrowsAsync<ExcepcionCalificaciones>(() =>
            servicio.CrearAsync(
                conductor.Id,
                viaje.Id,
                new SolicitudCrearCalificacion
                {
                    CalificadoId = pax.Id,
                    Estrellas = 5
                }));

        Assert.Equal(422, ex.CodigoEstado);
        Assert.Equal("rating_window_expired", ex.Codigo);
    }

    [Fact]
    public async Task Doble_calificacion_mismo_viaje_y_persona_devuelve_409()
    {
        await using var db = await CrearDbConSeedAsync();
        var (viaje, _, pax) = await CrearViajeCompletadoAsync(
            db,
            completadoEn: DateTimeOffset.UtcNow.AddHours(-1));

        var servicio = CrearServicio(db, pax);
        var solicitud = new SolicitudCrearCalificacion
        {
            CalificadoId = viaje.ConductorId,
            Estrellas = 4,
            Comentario = "Bien"
        };

        await servicio.CrearAsync(pax.Id, viaje.Id, solicitud);

        var ex = await Assert.ThrowsAsync<ExcepcionCalificaciones>(() =>
            servicio.CrearAsync(pax.Id, viaje.Id, solicitud));

        Assert.Equal(409, ex.CodigoEstado);
        Assert.Equal("rating_already_exists", ex.Codigo);
    }

    [Fact]
    public async Task Promedio_se_recomputa_correctamente_tras_calificaciones()
    {
        await using var db = await CrearDbConSeedAsync();
        // driver3 no tiene calificaciones en el seed.
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver3@utn.local");
        var pax1 = await db.Usuarios.SingleAsync(u => u.Correo == "pax1@utn.local");
        var pax2 = await db.Usuarios.SingleAsync(u => u.Correo == "pax2@utn.local");
        var campus = await db.Sedes.FirstAsync(c => c.Id == conductor.CampusId);

        var viaje1 = NuevoViajeCompletado(conductor, campus, DateTimeOffset.UtcNow.AddHours(-2));
        var viaje2 = NuevoViajeCompletado(conductor, campus, DateTimeOffset.UtcNow.AddHours(-1));
        db.Viajes.AddRange(viaje1, viaje2);
        await db.SaveChangesAsync();

        db.SolicitudesViaje.AddRange(
            NuevaSolicitud(viaje1, pax1),
            NuevaSolicitud(viaje2, pax2));
        await db.SaveChangesAsync();

        var servicioPax1 = CrearServicio(db, pax1);
        await servicioPax1.CrearAsync(
            pax1.Id,
            viaje1.Id,
            new SolicitudCrearCalificacion { CalificadoId = conductor.Id, Estrellas = 5 });

        var servicioPax2 = CrearServicio(db, pax2);
        await servicioPax2.CrearAsync(
            pax2.Id,
            viaje2.Id,
            new SolicitudCrearCalificacion { CalificadoId = conductor.Id, Estrellas = 3 });

        await db.Entry(conductor).ReloadAsync();
        Assert.Equal(4.00m, conductor.PromedioCalificacion);
        Assert.Equal(2, await db.Calificaciones.CountAsync(c => c.CalificadoId == conductor.Id));
    }

    [Fact]
    public async Task Lista_pendientes_se_vacia_tras_calificar()
    {
        await using var db = await CrearDbConSeedAsync();
        var (viaje, _, pax) = await CrearViajeCompletadoAsync(
            db,
            completadoEn: DateTimeOffset.UtcNow.AddHours(-1));

        var servicio = CrearServicio(db, pax);
        var pendientesAntes = await servicio.ListarPendientesAsync(pax.Id);
        Assert.Contains(pendientesAntes, p => p.ViajeId == viaje.Id && p.RolACalificar == "driver");

        await servicio.CrearAsync(
            pax.Id,
            viaje.Id,
            new SolicitudCrearCalificacion { CalificadoId = viaje.ConductorId, Estrellas = 5 });

        var pendientesDespues = await servicio.ListarPendientesAsync(pax.Id);
        Assert.DoesNotContain(pendientesDespues, p => p.ViajeId == viaje.Id);
    }

    [Fact]
    public async Task Conductor_bajo_minimo_con_al_menos_5_calificaciones_queda_auto_bloqueado()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver2@utn.local");
        var campus = await db.Sedes.FirstAsync(c => c.Id == conductor.CampusId);
        var config = await db.ConfiguracionesUniversidad.SingleAsync(c =>
            c.UniversidadId == conductor.UniversidadId);
        config.CalificacionMinimaConductor = 3.5m;
        await db.SaveChangesAsync();

        var cache = new MemoryCache(new MemoryCacheOptions());
        var cacheEstado = new CacheEstadoUsuario(db, cache);
        _ = await cacheEstado.ObtenerAsync(conductor.Id);

        var pasajeros = await db.Usuarios
            .Where(u => u.Rol == RolUsuario.Pasajero && u.UniversidadId == conductor.UniversidadId)
            .Take(2)
            .ToListAsync();
        Assert.True(pasajeros.Count >= 2);

        // 4 calificaciones previas con 1 estrella (avg=1), sin llegar a 5.
        for (var i = 0; i < 4; i++)
        {
            var viajePrev = NuevoViajeCompletado(conductor, campus, DateTimeOffset.UtcNow.AddDays(-(i + 2)));
            db.Viajes.Add(viajePrev);
            await db.SaveChangesAsync();
            db.SolicitudesViaje.Add(NuevaSolicitud(viajePrev, pasajeros[i % pasajeros.Count]));
            db.Calificaciones.Add(new Calificacion
            {
                UniversidadId = viajePrev.UniversidadId,
                ViajeId = viajePrev.Id,
                CalificadorId = pasajeros[i % pasajeros.Count].Id,
                CalificadoId = conductor.Id,
                Estrellas = 1
            });
            await db.SaveChangesAsync();
        }

        await RecomputarPromedioManualAsync(db, conductor.Id);
        await db.Entry(conductor).ReloadAsync();
        Assert.Equal(EstadoUsuario.Activo, conductor.Estado);
        Assert.Equal(1.00m, conductor.PromedioCalificacion);

        var (viaje, _, pax) = await CrearViajeCompletadoAsync(
            db,
            conductorCorreo: "driver2@utn.local",
            pasajeroCorreo: pasajeros[0].Correo,
            completadoEn: DateTimeOffset.UtcNow.AddHours(-1));

        var servicio = CrearServicio(db, pax, cacheEstado);
        await servicio.CrearAsync(
            pax.Id,
            viaje.Id,
            new SolicitudCrearCalificacion { CalificadoId = conductor.Id, Estrellas = 1 });

        await db.Entry(conductor).ReloadAsync();
        Assert.Equal(EstadoUsuario.Bloqueado, conductor.Estado);
        Assert.Equal(1.00m, conductor.PromedioCalificacion);

        var evento = await db.EventosAuditoria.SingleAsync(e =>
            e.Accion == $"user.auto_blocked_min_rating:{conductor.Id}");
        Assert.Equal(TipoEventoAuditoria.Sistema, evento.Tipo);
        Assert.Equal(SeveridadAuditoria.Alta, evento.Severidad);

        var notificacion = await db.Notificaciones.SingleAsync(n =>
            n.Tipo == TipoNotificacion.Bloqueo
            && n.RolDestinatario == RolUsuario.Coordinador
            && n.Cuerpo.Contains(conductor.Nombre));
        Assert.False(string.IsNullOrWhiteSpace(notificacion.Titulo));

        var sesion = await cacheEstado.ObtenerAsync(conductor.Id);
        Assert.False(sesion.Permitido);
        Assert.Equal("blocked", sesion.EstadoUsuario);
    }

    [Fact]
    public async Task Conductor_bajo_minimo_con_menos_de_5_calificaciones_no_se_bloquea()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver3@utn.local");
        var campus = await db.Sedes.FirstAsync(c => c.Id == conductor.CampusId);
        var config = await db.ConfiguracionesUniversidad.SingleAsync(c =>
            c.UniversidadId == conductor.UniversidadId);
        config.CalificacionMinimaConductor = 3.5m;
        await db.SaveChangesAsync();

        var pax1 = await db.Usuarios.SingleAsync(u => u.Correo == "pax1@utn.local");
        var pax2 = await db.Usuarios.SingleAsync(u => u.Correo == "pax2@utn.local");

        var viaje1 = NuevoViajeCompletado(conductor, campus, DateTimeOffset.UtcNow.AddHours(-3));
        var viaje2 = NuevoViajeCompletado(conductor, campus, DateTimeOffset.UtcNow.AddHours(-2));
        db.Viajes.AddRange(viaje1, viaje2);
        await db.SaveChangesAsync();
        db.SolicitudesViaje.AddRange(NuevaSolicitud(viaje1, pax1), NuevaSolicitud(viaje2, pax2));
        await db.SaveChangesAsync();

        var servicio1 = CrearServicio(db, pax1);
        await servicio1.CrearAsync(
            pax1.Id,
            viaje1.Id,
            new SolicitudCrearCalificacion { CalificadoId = conductor.Id, Estrellas = 1 });

        var servicio2 = CrearServicio(db, pax2);
        await servicio2.CrearAsync(
            pax2.Id,
            viaje2.Id,
            new SolicitudCrearCalificacion { CalificadoId = conductor.Id, Estrellas = 1 });

        // Llegamos a 2 (<5) con avg 1.0 < 3.5 → no bloquea.
        var viaje3 = NuevoViajeCompletado(conductor, campus, DateTimeOffset.UtcNow.AddHours(-1));
        db.Viajes.Add(viaje3);
        await db.SaveChangesAsync();
        db.SolicitudesViaje.Add(NuevaSolicitud(viaje3, pax1));
        await db.SaveChangesAsync();

        // Usar otra calificadora: pax2 en viaje3 no aplica; crear 2 más con seed calificaciones
        // vía el servicio con pax1 en un 3er viaje y pax2 en un 4º = 4 ratings.
        var viaje4 = NuevoViajeCompletado(conductor, campus, DateTimeOffset.UtcNow.AddMinutes(-30));
        db.Viajes.Add(viaje4);
        await db.SaveChangesAsync();
        db.SolicitudesViaje.Add(NuevaSolicitud(viaje4, pax2));
        await db.SaveChangesAsync();

        await servicio1.CrearAsync(
            pax1.Id,
            viaje3.Id,
            new SolicitudCrearCalificacion { CalificadoId = conductor.Id, Estrellas = 1 });
        await servicio2.CrearAsync(
            pax2.Id,
            viaje4.Id,
            new SolicitudCrearCalificacion { CalificadoId = conductor.Id, Estrellas = 1 });

        await db.Entry(conductor).ReloadAsync();
        Assert.Equal(4, await db.Calificaciones.CountAsync(c => c.CalificadoId == conductor.Id));
        Assert.Equal(1.00m, conductor.PromedioCalificacion);
        Assert.Equal(EstadoUsuario.Activo, conductor.Estado);
        Assert.False(await db.EventosAuditoria.AnyAsync(e =>
            e.Accion == $"user.auto_blocked_min_rating:{conductor.Id}"));
        Assert.False(await db.Notificaciones.AnyAsync(n =>
            n.Tipo == TipoNotificacion.Bloqueo && n.Cuerpo.Contains(conductor.Nombre)));
    }

    private static async Task RecomputarPromedioManualAsync(ContextoApp db, Guid calificadoId)
    {
        var usuario = await db.Usuarios.SingleAsync(u => u.Id == calificadoId);
        var avg = await db.Calificaciones
            .Where(c => c.CalificadoId == calificadoId)
            .AverageAsync(c => (decimal)c.Estrellas);
        usuario.PromedioCalificacion = Math.Round(avg, 2, MidpointRounding.AwayFromZero);
        await db.SaveChangesAsync();
    }

    private static async Task<(Viaje Viaje, Usuario Conductor, Usuario Pasajero)> CrearViajeCompletadoAsync(
        ContextoApp db,
        DateTimeOffset completadoEn,
        string conductorCorreo = "driver1@utn.local",
        string pasajeroCorreo = "pax1@utn.local")
    {
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == conductorCorreo);
        var pax = await db.Usuarios.SingleAsync(u => u.Correo == pasajeroCorreo);
        var campus = await db.Sedes.FirstAsync(c => c.Id == conductor.CampusId);
        var viaje = NuevoViajeCompletado(conductor, campus, completadoEn);
        db.Viajes.Add(viaje);
        await db.SaveChangesAsync();
        db.SolicitudesViaje.Add(NuevaSolicitud(viaje, pax));
        await db.SaveChangesAsync();
        return (viaje, conductor, pax);
    }

    private static Viaje NuevoViajeCompletado(
        Usuario conductor,
        Campus campus,
        DateTimeOffset completadoEn) =>
        new()
        {
            UniversidadId = conductor.UniversidadId!.Value,
            ConductorId = conductor.Id,
            CampusDestinoId = campus.Id,
            OrigenTexto = "Origen rating",
            OrigenLat = -0.35,
            OrigenLng = -78.12,
            SaleEn = completadoEn.AddHours(-1),
            AsientosDisponibles = 2,
            DistanciaKm = 5m,
            Estado = EstadoViaje.Completado,
            IniciadoEn = completadoEn.AddMinutes(-40),
            CompletadoEn = completadoEn
        };

    private static SolicitudViaje NuevaSolicitud(Viaje viaje, Usuario pasajero) =>
        new()
        {
            UniversidadId = viaje.UniversidadId,
            ViajeId = viaje.Id,
            PasajeroId = pasajero.Id,
            RecogidaTexto = "Parada",
            RecogidaLat = -0.351,
            RecogidaLng = -78.121,
            Estado = EstadoSolicitudViaje.Aceptada
        };

    private static IServicioCalificaciones CrearServicio(
        ContextoApp db,
        Usuario usuario,
        ICacheEstadoUsuario? cacheEstado = null)
    {
        var inquilino = new ContextoInquilino
        {
            OmitirFiltros = false,
            UniversidadId = usuario.UniversidadId,
            CampusId = usuario.CampusId,
            UsuarioId = usuario.Id,
            Rol = usuario.Rol
        };
        var auditoria = new EscritorAuditoria(db, inquilino);
        cacheEstado ??= new CacheEstadoUsuario(db, new MemoryCache(new MemoryCacheOptions()));
        return new ServicioCalificaciones(db, auditoria, new MotorEcoTokensStub(), cacheEstado);
    }

    private static async Task<ContextoApp> CrearDbConSeedAsync()
    {
        var db = CrearDb();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SUPER_ADMIN_EMAIL"] = "superadmin@kubix.local",
                ["SUPER_ADMIN_PASSWORD"] = SembradorBaseDatos.ContrasenaPorDefecto
            })
            .Build();

        var sembrador = new SembradorBaseDatos(db, config, NullLogger<SembradorBaseDatos>.Instance);
        await sembrador.SembrarAsync();
        return db;
    }

    private static ContextoApp CrearDb()
    {
        var opciones = new DbContextOptionsBuilder<ContextoApp>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ContextoApp(opciones, new ContextoInquilino { OmitirFiltros = true });
    }
}
