using Kubix.Application.Tenancy;
using Kubix.Application.Viajes;
using Kubix.Domain;
using Kubix.Domain.Entities;
using Kubix.Domain.Enums;
using Kubix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kubix.Infrastructure.Viajes;

public sealed class ServicioSolicitudesViaje(
    ContextoApp db,
    IContextoInquilino inquilino,
    IEscritorAuditoria auditoria) : IServicioSolicitudesViaje
{
    public async Task<IReadOnlyList<ViajeDto>> ListarDisponiblesAsync(
        Guid usuarioId,
        CancellationToken ct = default)
    {
        var pasajero = await ObtenerUsuarioAsync(usuarioId, ct);
        AsegurarPasajero(pasajero);

        var campusId = pasajero.CampusId ?? inquilino.CampusId;
        if (campusId is not Guid campusPasajero)
        {
            throw ExcepcionViajes.Validacion(
                "Passenger has no campus assigned.",
                "missing_campus");
        }

        var ahora = DateTimeOffset.UtcNow;
        var viajes = await db.Viajes
            .AsNoTracking()
            .Where(v =>
                v.CampusDestinoId == campusPasajero
                && v.Estado == EstadoViaje.Programado
                && v.AsientosDisponibles > 0
                && v.SaleEn > ahora)
            .OrderBy(v => v.SaleEn)
            .ToListAsync(ct);

        return viajes.Select(MapearViaje).ToList();
    }

    public async Task<SolicitudViajeDto> CrearSolicitudAsync(
        Guid usuarioId,
        Guid viajeId,
        SolicitudCrearSolicitudViaje solicitud,
        CancellationToken ct = default)
    {
        var pasajero = await ObtenerUsuarioAsync(usuarioId, ct);
        AsegurarPasajero(pasajero);

        if (pasajero.UniversidadId is not Guid universidadId)
        {
            throw ExcepcionViajes.Validacion("Passenger has no university.", "missing_university");
        }

        var campusId = pasajero.CampusId ?? inquilino.CampusId;
        if (campusId is not Guid campusPasajero)
        {
            throw ExcepcionViajes.Validacion(
                "Passenger has no campus assigned.",
                "missing_campus");
        }

        var recogidaTexto = (solicitud.RecogidaTexto ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(recogidaTexto))
        {
            throw ExcepcionViajes.Validacion("pickupText is required.");
        }

        ValidarCoordenadasRecogida(solicitud.RecogidaLat, solicitud.RecogidaLng);

        var viaje = await db.Viajes.FirstOrDefaultAsync(v => v.Id == viajeId, ct)
            ?? throw ExcepcionViajes.NoEncontrado("Trip not found.", "trip_not_found");

        if (!EsViajeDisponibleParaPasajero(viaje, campusPasajero))
        {
            throw ExcepcionViajes.Validacion(
                "Trip is not available for requests.",
                "trip_not_available");
        }

        var duplicada = await db.SolicitudesViaje.AnyAsync(
            s => s.ViajeId == viajeId && s.PasajeroId == usuarioId,
            ct);
        if (duplicada)
        {
            throw ExcepcionViajes.Conflicto(
                "A request for this trip already exists.",
                "duplicate_request");
        }

        var ahora = DateTimeOffset.UtcNow;
        var entidad = new SolicitudViaje
        {
            UniversidadId = universidadId,
            ViajeId = viajeId,
            PasajeroId = usuarioId,
            RecogidaTexto = recogidaTexto,
            RecogidaLat = solicitud.RecogidaLat,
            RecogidaLng = solicitud.RecogidaLng,
            Estado = EstadoSolicitudViaje.Pendiente,
            CreadoEn = ahora,
            ActualizadoEn = ahora
        };

        db.SolicitudesViaje.Add(entidad);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw ExcepcionViajes.Conflicto(
                "A request for this trip already exists.",
                "duplicate_request");
        }

        await IntentarAuditoriaAsync(
            $"ride_request.created:{entidad.Id}",
            universidadId,
            usuarioId,
            ct);

        return MapearSolicitud(entidad);
    }

    public async Task<IReadOnlyList<SolicitudViajeDto>> ListarPorViajeAsync(
        Guid usuarioId,
        Guid viajeId,
        CancellationToken ct = default)
    {
        var conductor = await ObtenerUsuarioAsync(usuarioId, ct);
        AsegurarConductor(conductor);

        var viaje = await db.Viajes.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == viajeId, ct)
            ?? throw ExcepcionViajes.NoEncontrado("Trip not found.", "trip_not_found");

        if (viaje.ConductorId != usuarioId)
        {
            throw ExcepcionViajes.NoEncontrado("Trip not found.", "trip_not_found");
        }

        var solicitudes = await db.SolicitudesViaje
            .AsNoTracking()
            .Where(s => s.ViajeId == viajeId)
            .OrderBy(s => s.CreadoEn)
            .ToListAsync(ct);

        return solicitudes.Select(MapearSolicitud).ToList();
    }

    public async Task<SolicitudViajeDto> AceptarAsync(
        Guid usuarioId,
        Guid solicitudId,
        CancellationToken ct = default)
    {
        var conductor = await ObtenerUsuarioAsync(usuarioId, ct);
        AsegurarConductor(conductor);

        await using var tx = await IniciarTransaccionSiSoportadaAsync(ct);

        var solicitud = await db.SolicitudesViaje
            .FirstOrDefaultAsync(s => s.Id == solicitudId, ct)
            ?? throw ExcepcionViajes.NoEncontrado("Ride request not found.", "request_not_found");

        var viaje = await db.Viajes
            .FirstOrDefaultAsync(v => v.Id == solicitud.ViajeId, ct)
            ?? throw ExcepcionViajes.NoEncontrado("Trip not found.", "trip_not_found");

        if (viaje.ConductorId != usuarioId)
        {
            throw ExcepcionViajes.NoEncontrado("Ride request not found.", "request_not_found");
        }

        if (solicitud.Estado != EstadoSolicitudViaje.Pendiente)
        {
            throw ExcepcionViajes.Validacion(
                "Only pending requests can be accepted.",
                "invalid_request_status");
        }

        if (viaje.Estado != EstadoViaje.Programado)
        {
            throw ExcepcionViajes.Validacion(
                "Trip is not available for accepting requests.",
                "trip_not_available");
        }

        if (viaje.AsientosDisponibles <= 0)
        {
            throw ExcepcionViajes.Conflicto(
                "No seats available on this trip.",
                "no_seats_available");
        }

        var ahora = DateTimeOffset.UtcNow;
        solicitud.Estado = EstadoSolicitudViaje.Aceptada;
        solicitud.ActualizadoEn = ahora;

        viaje.AsientosDisponibles -= 1;
        viaje.ActualizadoEn = ahora;

        if (viaje.AsientosDisponibles == 0)
        {
            var pendientes = await db.SolicitudesViaje
                .Where(s =>
                    s.ViajeId == viaje.Id
                    && s.Id != solicitud.Id
                    && s.Estado == EstadoSolicitudViaje.Pendiente)
                .ToListAsync(ct);

            foreach (var pendiente in pendientes)
            {
                pendiente.Estado = EstadoSolicitudViaje.Rechazada;
                pendiente.ActualizadoEn = ahora;
            }
        }

        await db.SaveChangesAsync(ct);
        if (tx is not null)
        {
            await tx.CommitAsync(ct);
        }

        await IntentarAuditoriaAsync(
            $"ride_request.accepted:{solicitud.Id}",
            viaje.UniversidadId,
            usuarioId,
            ct);

        return MapearSolicitud(solicitud);
    }

    public async Task<SolicitudViajeDto> RechazarAsync(
        Guid usuarioId,
        Guid solicitudId,
        CancellationToken ct = default)
    {
        var conductor = await ObtenerUsuarioAsync(usuarioId, ct);
        AsegurarConductor(conductor);

        var solicitud = await db.SolicitudesViaje
            .FirstOrDefaultAsync(s => s.Id == solicitudId, ct)
            ?? throw ExcepcionViajes.NoEncontrado("Ride request not found.", "request_not_found");

        var viaje = await db.Viajes.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == solicitud.ViajeId, ct)
            ?? throw ExcepcionViajes.NoEncontrado("Trip not found.", "trip_not_found");

        if (viaje.ConductorId != usuarioId)
        {
            throw ExcepcionViajes.NoEncontrado("Ride request not found.", "request_not_found");
        }

        if (solicitud.Estado != EstadoSolicitudViaje.Pendiente)
        {
            throw ExcepcionViajes.Validacion(
                "Only pending requests can be rejected.",
                "invalid_request_status");
        }

        var ahora = DateTimeOffset.UtcNow;
        solicitud.Estado = EstadoSolicitudViaje.Rechazada;
        solicitud.ActualizadoEn = ahora;
        await db.SaveChangesAsync(ct);

        await IntentarAuditoriaAsync(
            $"ride_request.rejected:{solicitud.Id}",
            viaje.UniversidadId,
            usuarioId,
            ct);

        return MapearSolicitud(solicitud);
    }

    public async Task<SolicitudViajeDto> CancelarAsync(
        Guid usuarioId,
        Guid solicitudId,
        CancellationToken ct = default)
    {
        var pasajero = await ObtenerUsuarioAsync(usuarioId, ct);
        AsegurarPasajero(pasajero);

        await using var tx = await IniciarTransaccionSiSoportadaAsync(ct);

        var solicitud = await db.SolicitudesViaje
            .FirstOrDefaultAsync(s => s.Id == solicitudId, ct)
            ?? throw ExcepcionViajes.NoEncontrado("Ride request not found.", "request_not_found");

        if (solicitud.PasajeroId != usuarioId)
        {
            throw ExcepcionViajes.NoEncontrado("Ride request not found.", "request_not_found");
        }

        if (solicitud.Estado is not (EstadoSolicitudViaje.Pendiente or EstadoSolicitudViaje.Aceptada))
        {
            throw ExcepcionViajes.Validacion(
                "Only pending or accepted requests can be cancelled.",
                "invalid_request_status");
        }

        var ahora = DateTimeOffset.UtcNow;
        var eraAceptada = solicitud.Estado == EstadoSolicitudViaje.Aceptada;

        solicitud.Estado = EstadoSolicitudViaje.CanceladaPorPasajero;
        solicitud.ActualizadoEn = ahora;

        if (eraAceptada)
        {
            var viaje = await db.Viajes
                .FirstOrDefaultAsync(v => v.Id == solicitud.ViajeId, ct)
                ?? throw ExcepcionViajes.NoEncontrado("Trip not found.", "trip_not_found");

            viaje.AsientosDisponibles += 1;
            viaje.ActualizadoEn = ahora;
        }

        await db.SaveChangesAsync(ct);
        if (tx is not null)
        {
            await tx.CommitAsync(ct);
        }

        await IntentarAuditoriaAsync(
            $"ride_request.cancelled:{solicitud.Id}",
            solicitud.UniversidadId,
            usuarioId,
            ct);

        return MapearSolicitud(solicitud);
    }

    private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?> IniciarTransaccionSiSoportadaAsync(
        CancellationToken ct)
    {
        if (!db.Database.IsRelational())
        {
            return null;
        }

        return await db.Database.BeginTransactionAsync(ct);
    }

    private async Task IntentarAuditoriaAsync(
        string accion,
        Guid universidadId,
        Guid usuarioId,
        CancellationToken ct)
    {
        try
        {
            await auditoria.EscribirAsync(
                accion,
                TipoEventoAuditoria.Sistema,
                SeveridadAuditoria.Baja,
                universidadId,
                usuarioId,
                ct: ct);
        }
        catch
        {
            // Auditoría opcional.
        }
    }

    private async Task<Usuario> ObtenerUsuarioAsync(Guid usuarioId, CancellationToken ct)
    {
        return await db.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == usuarioId, ct)
            ?? throw ExcepcionViajes.NoEncontrado("User not found.", "user_not_found");
    }

    private static void AsegurarPasajero(Usuario usuario)
    {
        if (usuario.Rol != RolUsuario.Pasajero)
        {
            throw ExcepcionViajes.Prohibido(
                "Only passengers can perform this action.",
                "passenger_only");
        }
    }

    private static void AsegurarConductor(Usuario usuario)
    {
        if (usuario.Rol != RolUsuario.Conductor)
        {
            throw ExcepcionViajes.Prohibido(
                "Only drivers can perform this action.",
                "driver_only");
        }
    }

    private static bool EsViajeDisponibleParaPasajero(Viaje viaje, Guid campusPasajero) =>
        viaje.CampusDestinoId == campusPasajero
        && viaje.Estado == EstadoViaje.Programado
        && viaje.AsientosDisponibles > 0
        && viaje.SaleEn > DateTimeOffset.UtcNow;

    private static void ValidarCoordenadasRecogida(double lat, double lng)
    {
        if (lat is < -90 or > 90 || lng is < -180 or > 180)
        {
            throw ExcepcionViajes.Validacion(
                "pickupLat must be in [-90, 90] and pickupLng in [-180, 180].",
                "invalid_pickup");
        }
    }

    private static ViajeDto MapearViaje(Viaje v) => new()
    {
        Id = v.Id,
        Estado = ConversorEnumDominio.ACadenaDb(v.Estado),
        OrigenTexto = v.OrigenTexto,
        OrigenLat = v.OrigenLat,
        OrigenLng = v.OrigenLng,
        CampusDestinoId = v.CampusDestinoId,
        SaleEn = v.SaleEn,
        AsientosDisponibles = v.AsientosDisponibles,
        Polilinea = v.Polilinea,
        DistanciaKm = v.DistanciaKm,
        ConductorId = v.ConductorId,
        UniversidadId = v.UniversidadId
    };

    private static SolicitudViajeDto MapearSolicitud(SolicitudViaje s) => new()
    {
        Id = s.Id,
        ViajeId = s.ViajeId,
        PasajeroId = s.PasajeroId,
        RecogidaTexto = s.RecogidaTexto,
        RecogidaLat = s.RecogidaLat,
        RecogidaLng = s.RecogidaLng,
        Estado = ConversorEnumDominio.ACadenaDb(s.Estado),
        UniversidadId = s.UniversidadId
    };
}
