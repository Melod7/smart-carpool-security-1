using Kubix.Application.Tenancy;
using Kubix.Application.Viajes;
using Kubix.Domain.Entities;
using Kubix.Domain.Enums;
using Kubix.Infrastructure.Persistence;
using Kubix.Infrastructure.Seeding;
using Kubix.Infrastructure.Tenancy;
using Kubix.Infrastructure.Viajes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kubix.Tests;

public class PruebasSolicitudesViaje
{
    [Fact]
    public async Task Pasajero_otro_campus_no_ve_viaje_disponible()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver1@utn.local");
        var campusIbarra = await db.Sedes.SingleAsync(c =>
            c.UniversidadId == conductor.UniversidadId && c.Nombre == "Campus Ibarra");
        var pasajeroOtavalo = await db.Usuarios.SingleAsync(u => u.Correo == "pax4@utn.local");
        var pasajeroIbarra = await db.Usuarios.SingleAsync(u => u.Correo == "pax1@utn.local");

        var viaje = new Viaje
        {
            UniversidadId = conductor.UniversidadId!.Value,
            ConductorId = conductor.Id,
            CampusDestinoId = campusIbarra.Id,
            OrigenTexto = "Solo Ibarra",
            OrigenLat = -0.35,
            OrigenLng = -78.12,
            SaleEn = DateTimeOffset.UtcNow.AddHours(8),
            AsientosDisponibles = 2,
            DistanciaKm = 5m,
            Estado = EstadoViaje.Programado
        };
        db.Viajes.Add(viaje);
        await db.SaveChangesAsync();

        var servicioOtavalo = CrearServicio(db, pasajeroOtavalo);
        var disponiblesOtavalo = await servicioOtavalo.ListarDisponiblesAsync(pasajeroOtavalo.Id);
        Assert.DoesNotContain(disponiblesOtavalo, v => v.Id == viaje.Id);

        var servicioIbarra = CrearServicio(db, pasajeroIbarra);
        var disponiblesIbarra = await servicioIbarra.ListarDisponiblesAsync(pasajeroIbarra.Id);
        Assert.Contains(disponiblesIbarra, v => v.Id == viaje.Id);
    }

    [Fact]
    public async Task Aceptar_ultimo_asiento_auto_rechaza_pending()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver2@utn.local");
        var campus = await db.Sedes.FirstAsync(c => c.Id == conductor.CampusId);
        var pax1 = await db.Usuarios.SingleAsync(u => u.Correo == "pax1@utn.local");
        var pax2 = await db.Usuarios.SingleAsync(u => u.Correo == "pax2@utn.local");
        var pax3 = await db.Usuarios.SingleAsync(u => u.Correo == "pax3@utn.local");

        var viaje = new Viaje
        {
            UniversidadId = conductor.UniversidadId!.Value,
            ConductorId = conductor.Id,
            CampusDestinoId = campus.Id,
            OrigenTexto = "Ultimo asiento",
            OrigenLat = -0.35,
            OrigenLng = -78.12,
            SaleEn = DateTimeOffset.UtcNow.AddHours(5),
            AsientosDisponibles = 1,
            DistanciaKm = 4m,
            Estado = EstadoViaje.Programado
        };
        db.Viajes.Add(viaje);
        await db.SaveChangesAsync();

        var s1 = NuevaPendiente(viaje, pax1, "Punto A");
        var s2 = NuevaPendiente(viaje, pax2, "Punto B");
        var s3 = NuevaPendiente(viaje, pax3, "Punto C");
        db.SolicitudesViaje.AddRange(s1, s2, s3);
        await db.SaveChangesAsync();

        var servicio = CrearServicio(db, conductor);
        var aceptada = await servicio.AceptarAsync(conductor.Id, s1.Id);

        Assert.Equal("accepted", aceptada.Estado);

        var enDb = await db.Viajes.SingleAsync(v => v.Id == viaje.Id);
        Assert.Equal(0, enDb.AsientosDisponibles);

        var estados = await db.SolicitudesViaje
            .Where(s => s.ViajeId == viaje.Id)
            .ToDictionaryAsync(s => s.Id, s => s.Estado);

        Assert.Equal(EstadoSolicitudViaje.Aceptada, estados[s1.Id]);
        Assert.Equal(EstadoSolicitudViaje.Rechazada, estados[s2.Id]);
        Assert.Equal(EstadoSolicitudViaje.Rechazada, estados[s3.Id]);
    }

    [Fact]
    public async Task Solicitud_duplicada_devuelve_409()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver1@utn.local");
        var campus = await db.Sedes.FirstAsync(c => c.Id == conductor.CampusId);
        var pasajero = await db.Usuarios.SingleAsync(u => u.Correo == "pax3@utn.local");

        var viaje = new Viaje
        {
            UniversidadId = conductor.UniversidadId!.Value,
            ConductorId = conductor.Id,
            CampusDestinoId = campus.Id,
            OrigenTexto = "Duplicado",
            OrigenLat = -0.35,
            OrigenLng = -78.12,
            SaleEn = DateTimeOffset.UtcNow.AddHours(4),
            AsientosDisponibles = 2,
            DistanciaKm = 3m,
            Estado = EstadoViaje.Programado
        };
        db.Viajes.Add(viaje);
        await db.SaveChangesAsync();

        var servicio = CrearServicio(db, pasajero);
        var body = new SolicitudCrearSolicitudViaje
        {
            RecogidaTexto = "Esquina norte",
            RecogidaLat = -0.351,
            RecogidaLng = -78.122
        };

        await servicio.CrearSolicitudAsync(pasajero.Id, viaje.Id, body);

        var ex = await Assert.ThrowsAsync<ExcepcionViajes>(() =>
            servicio.CrearSolicitudAsync(pasajero.Id, viaje.Id, body));

        Assert.Equal(409, ex.CodigoEstado);
        Assert.Equal("duplicate_request", ex.Codigo);
    }

    [Fact]
    public async Task Cancelar_aceptada_restaura_asiento_y_reaparece_en_available()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver3@utn.local");
        var campus = await db.Sedes.FirstAsync(c => c.Id == conductor.CampusId);
        var pasajero = await db.Usuarios.SingleAsync(u => u.Correo == "pax4@utn.local");

        var viaje = new Viaje
        {
            UniversidadId = conductor.UniversidadId!.Value,
            ConductorId = conductor.Id,
            CampusDestinoId = campus.Id,
            OrigenTexto = "Cancel restore",
            OrigenLat = 0.23,
            OrigenLng = -78.26,
            SaleEn = DateTimeOffset.UtcNow.AddHours(7),
            AsientosDisponibles = 1,
            DistanciaKm = 6m,
            Estado = EstadoViaje.Programado
        };
        db.Viajes.Add(viaje);
        await db.SaveChangesAsync();

        var solicitud = NuevaPendiente(viaje, pasajero, "Parada Otavalo");
        db.SolicitudesViaje.Add(solicitud);
        await db.SaveChangesAsync();

        var servicioConductor = CrearServicio(db, conductor);
        await servicioConductor.AceptarAsync(conductor.Id, solicitud.Id);

        var viajeLleno = await db.Viajes.SingleAsync(v => v.Id == viaje.Id);
        Assert.Equal(0, viajeLleno.AsientosDisponibles);

        var servicioPasajero = CrearServicio(db, pasajero);
        var antes = await servicioPasajero.ListarDisponiblesAsync(pasajero.Id);
        Assert.DoesNotContain(antes, v => v.Id == viaje.Id);

        var cancelada = await servicioPasajero.CancelarAsync(pasajero.Id, solicitud.Id);
        Assert.Equal("cancelled_by_passenger", cancelada.Estado);

        var viajeRestaurado = await db.Viajes.SingleAsync(v => v.Id == viaje.Id);
        Assert.Equal(1, viajeRestaurado.AsientosDisponibles);

        var despues = await servicioPasajero.ListarDisponiblesAsync(pasajero.Id);
        Assert.Contains(despues, v => v.Id == viaje.Id);

        // Auto-rechazadas no se reactivan: no hay pendientes que reactivar en este caso,
        // pero el estado de la cancelada permanece cancelled_by_passenger.
        var enDb = await db.SolicitudesViaje.SingleAsync(s => s.Id == solicitud.Id);
        Assert.Equal(EstadoSolicitudViaje.CanceladaPorPasajero, enDb.Estado);
    }

    [Fact]
    public async Task Segundo_accept_sin_asientos_devuelve_409()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver1@utn.local");
        var campus = await db.Sedes.FirstAsync(c => c.Id == conductor.CampusId);
        var pax1 = await db.Usuarios.SingleAsync(u => u.Correo == "pax1@utn.local");
        var pax2 = await db.Usuarios.SingleAsync(u => u.Correo == "pax2@utn.local");

        var viaje = new Viaje
        {
            UniversidadId = conductor.UniversidadId!.Value,
            ConductorId = conductor.Id,
            CampusDestinoId = campus.Id,
            OrigenTexto = "Carrera de asientos",
            OrigenLat = -0.35,
            OrigenLng = -78.12,
            SaleEn = DateTimeOffset.UtcNow.AddHours(3),
            AsientosDisponibles = 1,
            DistanciaKm = 2m,
            Estado = EstadoViaje.Programado
        };
        db.Viajes.Add(viaje);
        await db.SaveChangesAsync();

        var s1 = NuevaPendiente(viaje, pax1, "A");
        var s2 = NuevaPendiente(viaje, pax2, "B");
        db.SolicitudesViaje.AddRange(s1, s2);
        await db.SaveChangesAsync();

        var servicio = CrearServicio(db, conductor);
        await servicio.AceptarAsync(conductor.Id, s1.Id);

        // Tras el primer accept, s2 queda auto-rechazada. Forzamos estado pending
        // para ejercitar el chequeo de asientos == 0 → 409.
        var s2Db = await db.SolicitudesViaje.SingleAsync(s => s.Id == s2.Id);
        s2Db.Estado = EstadoSolicitudViaje.Pendiente;
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ExcepcionViajes>(() =>
            servicio.AceptarAsync(conductor.Id, s2.Id));

        Assert.Equal(409, ex.CodigoEstado);
        Assert.Equal("no_seats_available", ex.Codigo);
    }

    private static SolicitudViaje NuevaPendiente(Viaje viaje, Usuario pasajero, string texto) =>
        new()
        {
            UniversidadId = viaje.UniversidadId,
            ViajeId = viaje.Id,
            PasajeroId = pasajero.Id,
            RecogidaTexto = texto,
            RecogidaLat = -0.35,
            RecogidaLng = -78.12,
            Estado = EstadoSolicitudViaje.Pendiente
        };

    private static IServicioSolicitudesViaje CrearServicio(ContextoApp db, Usuario usuario)
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
        return new ServicioSolicitudesViaje(db, inquilino, auditoria);
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
}
