using Kubix.Application.Tenancy;
using Kubix.Application.Viajes;
using Kubix.Domain.Entities;
using Kubix.Domain.Enums;
using Kubix.Infrastructure.EcoTokens;
using Kubix.Infrastructure.Persistence;
using Kubix.Infrastructure.Seeding;
using Kubix.Infrastructure.Tenancy;
using Kubix.Infrastructure.Viajes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kubix.Tests;

public class PruebasCicloViaje
{
    [Fact]
    public async Task Completar_programado_y_cancelar_en_curso_devuelven_409()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver1@utn.local");
        var campus = await db.Sedes.FirstAsync(c => c.Id == conductor.CampusId);
        var servicio = CrearServicio(db, conductor);

        var programado = NuevoViaje(conductor, campus, EstadoViaje.Programado, DateTimeOffset.UtcNow.AddHours(4));
        var enCurso = NuevoViaje(
            conductor,
            campus,
            EstadoViaje.EnCurso,
            DateTimeOffset.UtcNow.AddMinutes(-10),
            iniciadoEn: DateTimeOffset.UtcNow.AddMinutes(-5));
        db.Viajes.AddRange(programado, enCurso);
        await db.SaveChangesAsync();

        var exCompletar = await Assert.ThrowsAsync<ExcepcionViajes>(() =>
            servicio.CompletarViajeAsync(conductor.Id, programado.Id));
        Assert.Equal(409, exCompletar.CodigoEstado);
        Assert.Equal("invalid_trip_status", exCompletar.Codigo);

        var exCancelar = await Assert.ThrowsAsync<ExcepcionViajes>(() =>
            servicio.CancelarViajeAsync(conductor.Id, enCurso.Id));
        Assert.Equal(409, exCancelar.CodigoEstado);
        Assert.Equal("invalid_trip_status", exCancelar.Codigo);
    }

    [Fact]
    public async Task Cancelar_cascada_solicitudes_y_passenger_mine_lo_refleja()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver2@utn.local");
        var campus = await db.Sedes.FirstAsync(c => c.Id == conductor.CampusId);
        var pax1 = await db.Usuarios.SingleAsync(u => u.Correo == "pax1@utn.local");
        var pax2 = await db.Usuarios.SingleAsync(u => u.Correo == "pax2@utn.local");

        var viaje = NuevoViaje(conductor, campus, EstadoViaje.Programado, DateTimeOffset.UtcNow.AddHours(5));
        db.Viajes.Add(viaje);
        await db.SaveChangesAsync();

        var pendiente = NuevaSolicitud(viaje, pax1, EstadoSolicitudViaje.Pendiente);
        var aceptada = NuevaSolicitud(viaje, pax2, EstadoSolicitudViaje.Aceptada);
        db.SolicitudesViaje.AddRange(pendiente, aceptada);
        await db.SaveChangesAsync();

        var servicioConductor = CrearServicio(db, conductor);
        var cancelado = await servicioConductor.CancelarViajeAsync(conductor.Id, viaje.Id);
        Assert.Equal("cancelled", cancelado.Estado);

        var estados = await db.SolicitudesViaje
            .Where(s => s.ViajeId == viaje.Id)
            .ToDictionaryAsync(s => s.Id, s => s.Estado);
        Assert.Equal(EstadoSolicitudViaje.CanceladaPorConductor, estados[pendiente.Id]);
        Assert.Equal(EstadoSolicitudViaje.CanceladaPorConductor, estados[aceptada.Id]);

        var servicioPasajero = CrearServicio(db, pax1);
        var mios = await servicioPasajero.ListarMisViajesAsync(pax1.Id);
        var item = Assert.Single(mios.Viajes, t => t.Id == viaje.Id);
        Assert.Equal("passenger", item.Rol);
        Assert.Equal("cancelled", item.Estado);
        Assert.Equal("cancelled_by_driver", item.EstadoSolicitud);
    }

    [Fact]
    public async Task Cancelacion_tardia_escribe_evento_auditoria()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver3@utn.local");
        var campus = await db.Sedes.FirstAsync(c => c.Id == conductor.CampusId);
        var servicio = CrearServicio(db, conductor);

        var viaje = NuevoViaje(
            conductor,
            campus,
            EstadoViaje.Programado,
            DateTimeOffset.UtcNow.AddMinutes(20));
        db.Viajes.Add(viaje);
        await db.SaveChangesAsync();

        await servicio.CancelarViajeAsync(conductor.Id, viaje.Id);

        var evento = await db.EventosAuditoria
            .SingleAsync(e => e.Accion == $"trip.late_cancel:{viaje.Id}");
        Assert.Equal(TipoEventoAuditoria.Sistema, evento.Tipo);
        Assert.Equal(SeveridadAuditoria.Media, evento.Severidad);
        Assert.Equal(conductor.Id, evento.UsuarioId);
    }

    [Fact]
    public async Task Completar_calcula_co2_cuando_seguimiento_habilitado()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver1@utn.local");
        var campus = await db.Sedes.FirstAsync(c => c.Id == conductor.CampusId);
        var config = await db.ConfiguracionesUniversidad.SingleAsync(c => c.UniversidadId == conductor.UniversidadId);
        config.SeguimientoCo2Habilitado = true;
        config.FactorCo2KgKm = 0.17m;
        await db.SaveChangesAsync();

        var viaje = NuevoViaje(
            conductor,
            campus,
            EstadoViaje.EnCurso,
            DateTimeOffset.UtcNow.AddMinutes(-30),
            distanciaKm: 10m,
            iniciadoEn: DateTimeOffset.UtcNow.AddMinutes(-25));
        db.Viajes.Add(viaje);
        var pax = await db.Usuarios.FirstAsync(u =>
            u.UniversidadId == conductor.UniversidadId && u.Rol == RolUsuario.Pasajero);
        db.SolicitudesViaje.Add(new SolicitudViaje
        {
            UniversidadId = conductor.UniversidadId!.Value,
            ViajeId = viaje.Id,
            PasajeroId = pax.Id,
            RecogidaTexto = "Parada CO2",
            RecogidaLat = -0.35,
            RecogidaLng = -78.12,
            Estado = EstadoSolicitudViaje.Aceptada
        });
        await db.SaveChangesAsync();

        var servicio = CrearServicio(db, conductor);
        var completado = await servicio.CompletarViajeAsync(conductor.Id, viaje.Id);

        Assert.Equal("completed", completado.Estado);
        // 1 pasajero × 10 km × 0.17 = 1.7
        Assert.Equal(1.7m, completado.Co2AhorradoKg);

        var enDb = await db.Viajes.SingleAsync(v => v.Id == viaje.Id);
        Assert.Equal(EstadoViaje.Completado, enDb.Estado);
        Assert.NotNull(enDb.CompletadoEn);
        Assert.Equal(1.7m, enDb.Co2AhorradoKg);
    }

    [Fact]
    public async Task Passenger_mine_solo_muestra_viajes_solicitados()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver2@utn.local");
        var campus = await db.Sedes.FirstAsync(c => c.Id == conductor.CampusId);
        var pax1 = await db.Usuarios.SingleAsync(u => u.Correo == "pax1@utn.local");
        var pax2 = await db.Usuarios.SingleAsync(u => u.Correo == "pax2@utn.local");

        var viajePendiente = NuevoViaje(conductor, campus, EstadoViaje.Programado, DateTimeOffset.UtcNow.AddHours(3));
        var viajeAceptado = NuevoViaje(conductor, campus, EstadoViaje.Programado, DateTimeOffset.UtcNow.AddHours(6));
        var viajeAjeno = NuevoViaje(conductor, campus, EstadoViaje.Programado, DateTimeOffset.UtcNow.AddHours(8));
        db.Viajes.AddRange(viajePendiente, viajeAceptado, viajeAjeno);
        await db.SaveChangesAsync();

        db.SolicitudesViaje.AddRange(
            NuevaSolicitud(viajePendiente, pax1, EstadoSolicitudViaje.Pendiente),
            NuevaSolicitud(viajeAceptado, pax1, EstadoSolicitudViaje.Aceptada),
            NuevaSolicitud(viajeAjeno, pax2, EstadoSolicitudViaje.Pendiente));
        await db.SaveChangesAsync();

        var servicio = CrearServicio(db, pax1);
        var mios = await servicio.ListarMisViajesAsync(pax1.Id);

        var ids = mios.Viajes.Select(t => t.Id).ToHashSet();
        Assert.Contains(viajePendiente.Id, ids);
        Assert.Contains(viajeAceptado.Id, ids);
        Assert.DoesNotContain(viajeAjeno.Id, ids);

        var pendiente = Assert.Single(mios.Viajes, t => t.Id == viajePendiente.Id);
        Assert.Equal("pending", pendiente.EstadoSolicitud);
        Assert.Equal("passenger", pendiente.Rol);

        var aceptado = Assert.Single(mios.Viajes, t => t.Id == viajeAceptado.Id);
        Assert.Equal("accepted", aceptado.EstadoSolicitud);
    }

    private static Viaje NuevoViaje(
        Usuario conductor,
        Campus campus,
        EstadoViaje estado,
        DateTimeOffset saleEn,
        decimal distanciaKm = 5m,
        DateTimeOffset? iniciadoEn = null) =>
        new()
        {
            UniversidadId = conductor.UniversidadId!.Value,
            ConductorId = conductor.Id,
            CampusDestinoId = campus.Id,
            OrigenTexto = "Origen ciclo",
            OrigenLat = -0.35,
            OrigenLng = -78.12,
            SaleEn = saleEn,
            AsientosDisponibles = 2,
            DistanciaKm = distanciaKm,
            Estado = estado,
            IniciadoEn = iniciadoEn
        };

    private static SolicitudViaje NuevaSolicitud(
        Viaje viaje,
        Usuario pasajero,
        EstadoSolicitudViaje estado) =>
        new()
        {
            UniversidadId = viaje.UniversidadId,
            ViajeId = viaje.Id,
            PasajeroId = pasajero.Id,
            RecogidaTexto = "Parada",
            RecogidaLat = -0.351,
            RecogidaLng = -78.121,
            Estado = estado
        };

    private static IServicioViajes CrearServicio(ContextoApp db, Usuario usuario)
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
        return new ServicioViajes(db, new DirectionsNoOp(), auditoria, new MotorEcoTokensStub());
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
        await sembrador.SembrarDemoAsync();
        return db;
    }

    private static ContextoApp CrearDb()
    {
        var opciones = new DbContextOptionsBuilder<ContextoApp>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ContextoApp(opciones, new ContextoInquilino { OmitirFiltros = true });
    }

    private sealed class DirectionsNoOp : IServicioDirections
    {
        public Task<ResultadoDirections?> ObtenerRutaAsync(
            double origenLat,
            double origenLng,
            double destinoLat,
            double destinoLng,
            IReadOnlyList<(double Lat, double Lng)>? vias = null,
            CancellationToken ct = default) =>
            Task.FromResult<ResultadoDirections?>(null);
    }
}
