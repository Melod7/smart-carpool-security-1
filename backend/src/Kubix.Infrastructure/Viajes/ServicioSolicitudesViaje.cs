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
    IEscritorAuditoria auditoria,
    IServicioDirections directions) : IServicioSolicitudesViaje
{
    public async Task<IReadOnlyList<ViajeDto>> ListarDisponiblesAsync(
        Guid usuarioId,
        double? lat = null,
        double? lng = null,
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

        if ((lat is null) != (lng is null))
        {
            throw ExcepcionViajes.Validacion(
                "lat and lng must both be provided or both omitted.",
                "invalid_location");
        }

        if (lat is double latVal && lng is double lngVal)
        {
            ValidarCoordenadasRecogida(latVal, lngVal);
        }

        var ahora = DateTimeOffset.UtcNow;
        var viajes = await db.Viajes
            .Include(v => v.PuntosRuta)
            .Include(v => v.CampusDestino)
            .Where(v =>
                v.CampusDestinoId == campusPasajero
                && v.Estado == EstadoViaje.Programado
                && v.AsientosDisponibles > 0
                && v.SaleEn > ahora)
            .OrderBy(v => v.SaleEn)
            .ToListAsync(ct);

        foreach (var viaje in viajes)
        {
            await AsegurarPolilineaAsync(viaje, ct);
        }

        return viajes.Select(v =>
        {
            var dto = MapearViaje(v);
            if (lat is double pLat && lng is double pLng)
            {
                dto.EsperaSugerida = MapearEsperaSugerida(
                    CalcularEspera(v, pLat, pLng));
            }

            return dto;
        }).ToList();
    }

    public async Task<PuntoEsperaSugeridoDto> ObtenerPuntoEsperaSugeridoAsync(
        Guid usuarioId,
        Guid viajeId,
        double lat,
        double lng,
        CancellationToken ct = default)
    {
        var pasajero = await ObtenerUsuarioAsync(usuarioId, ct);
        AsegurarPasajero(pasajero);
        ValidarCoordenadasRecogida(lat, lng);

        var campusId = pasajero.CampusId ?? inquilino.CampusId;
        if (campusId is not Guid campusPasajero)
        {
            throw ExcepcionViajes.Validacion(
                "Passenger has no campus assigned.",
                "missing_campus");
        }

        var viaje = await db.Viajes
            .AsNoTracking()
            .Include(v => v.PuntosRuta)
            .Include(v => v.CampusDestino)
            .FirstOrDefaultAsync(v => v.Id == viajeId, ct)
            ?? throw ExcepcionViajes.NoEncontrado("Trip not found.", "trip_not_found");

        if (viaje.CampusDestinoId != campusPasajero)
        {
            throw ExcepcionViajes.Prohibido(
                "Trip is not available for this passenger campus.",
                "trip_not_available");
        }

        return MapearEsperaSugerida(CalcularEspera(viaje, lat, lng));
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

        var viaje = await db.Viajes
            .Include(v => v.PuntosRuta)
            .Include(v => v.CampusDestino)
            .FirstOrDefaultAsync(v => v.Id == viajeId, ct)
            ?? throw ExcepcionViajes.NoEncontrado("Trip not found.", "trip_not_found");

        if (!EsViajeDisponibleParaPasajero(viaje, campusPasajero))
        {
            throw ExcepcionViajes.Validacion(
                "Trip is not available for requests.",
                "trip_not_available");
        }

        var espera = CalcularEspera(viaje, solicitud.RecogidaLat, solicitud.RecogidaLng);
        if (espera.TooFar)
        {
            throw ExcepcionViajes.Validacion(
                "Pickup is too far from the driver route.",
                "pickup_too_far");
        }

        var recogidaLat = espera.Lat;
        var recogidaLng = espera.Lng;

        var existente = await db.SolicitudesViaje
            .FirstOrDefaultAsync(s => s.ViajeId == viajeId && s.PasajeroId == usuarioId, ct);

        if (existente is not null)
        {
            if (existente.Estado is not (
                EstadoSolicitudViaje.Rechazada
                or EstadoSolicitudViaje.CanceladaPorPasajero
                or EstadoSolicitudViaje.CanceladaPorConductor))
            {
                throw ExcepcionViajes.Conflicto(
                    "A request for this trip already exists.",
                    "duplicate_request");
            }

            var ahoraReintento = DateTimeOffset.UtcNow;
            existente.RecogidaTexto = recogidaTexto;
            existente.RecogidaLat = recogidaLat;
            existente.RecogidaLng = recogidaLng;
            existente.LatSugerida = espera.Lat;
            existente.LngSugerida = espera.Lng;
            existente.DistanciaARutaM = espera.DistanciaM;
            existente.Estado = EstadoSolicitudViaje.Pendiente;
            existente.ActualizadoEn = ahoraReintento;
            await db.SaveChangesAsync(ct);

            await IntentarAuditoriaAsync(
                $"ride_request.recreated:{existente.Id}",
                universidadId,
                usuarioId,
                ct);

            return MapearSolicitud(existente);
        }

        var ahora = DateTimeOffset.UtcNow;
        var entidad = new SolicitudViaje
        {
            UniversidadId = universidadId,
            ViajeId = viajeId,
            PasajeroId = usuarioId,
            RecogidaTexto = recogidaTexto,
            RecogidaLat = recogidaLat,
            RecogidaLng = recogidaLng,
            LatSugerida = espera.Lat,
            LngSugerida = espera.Lng,
            DistanciaARutaM = espera.DistanciaM,
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

    private async Task AsegurarPolilineaAsync(Viaje viaje, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(viaje.Polilinea))
        {
            return;
        }

        var campus = viaje.CampusDestino;
        if (campus is null)
        {
            return;
        }

        var waypoints = viaje.PuntosRuta.OrderBy(p => p.Seq).ToList();
        if (waypoints.Count < 2)
        {
            return;
        }

        var wp0 = waypoints[0];
        var vias = waypoints.Skip(1).Select(w => (w.Lat, w.Lng)).ToList();

        try
        {
            var resultado = await directions.ObtenerRutaAsync(
                wp0.Lat,
                wp0.Lng,
                campus.Lat,
                campus.Lng,
                vias,
                ct);

            if (resultado is null || string.IsNullOrWhiteSpace(resultado.Polilinea))
            {
                return;
            }

            viaje.Polilinea = resultado.Polilinea;
            if (resultado.DistanciaKm > 0)
            {
                viaje.DistanciaKm = resultado.DistanciaKm;
            }

            viaje.ActualizadoEn = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Preview diferido: no bloquear el listado si Directions falla.
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

    private static ResultadoPuntoEspera CalcularEspera(Viaje viaje, double lat, double lng)
    {
        var campus = viaje.CampusDestino
            ?? throw ExcepcionViajes.Validacion("Trip campus not loaded.", "campus_not_found");

        var waypoints = viaje.PuntosRuta
            .OrderBy(p => p.Seq)
            .Select(p => (p.Lat, p.Lng))
            .ToList();

        if (waypoints.Count == 0)
        {
            waypoints.Add((viaje.OrigenLat, viaje.OrigenLng));
        }

        return UtilidadPuntoEspera.Calcular(
            lat,
            lng,
            viaje.Polilinea,
            waypoints,
            campus.Lat,
            campus.Lng);
    }

    private static PuntoEsperaSugeridoDto MapearEsperaSugerida(ResultadoPuntoEspera r) => new()
    {
        Lat = r.Lat,
        Lng = r.Lng,
        DistanciaM = r.DistanciaM,
        SegmentIndex = r.SegmentIndex,
        TooFar = r.TooFar
    };

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
        Co2AhorradoKg = v.Co2AhorradoKg,
        ConductorId = v.ConductorId,
        UniversidadId = v.UniversidadId,
        Waypoints = v.PuntosRuta
            .OrderBy(p => p.Seq)
            .Select(p => new WaypointDto
            {
                Seq = p.Seq,
                Lat = p.Lat,
                Lng = p.Lng,
                Etiqueta = p.Etiqueta
            })
            .ToList()
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
