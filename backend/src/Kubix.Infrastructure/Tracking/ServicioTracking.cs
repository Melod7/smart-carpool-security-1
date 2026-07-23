using Kubix.Application.Tenancy;
using Kubix.Application.Tracking;
using Kubix.Domain;
using Kubix.Domain.Entities;
using Kubix.Domain.Enums;
using Kubix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kubix.Infrastructure.Tracking;

public sealed class ServicioTracking(
    ContextoApp db,
    IContextoInquilino inquilino) : IServicioTracking
{
    public async Task<PingDto> RegistrarPingAsync(
        Guid usuarioId,
        Guid viajeId,
        SolicitudPing solicitud,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ValidarCoordenadas(solicitud.Lat, solicitud.Lng);

        var viaje = await db.Viajes
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == viajeId, ct)
            ?? throw ExcepcionTracking.NoEncontrado("Trip not found.", "trip_not_found");

        if (viaje.Estado is not (EstadoViaje.Programado or EstadoViaje.EnCurso))
        {
            throw ExcepcionTracking.Conflicto(
                "Pings are only allowed on scheduled or in-progress trips.",
                "trip_not_active");
        }

        var esParticipante = await EsParticipanteAsync(viaje, usuarioId, ct);
        if (!esParticipante)
        {
            throw ExcepcionTracking.Prohibido(
                "Only trip participants can send location pings.",
                "not_participant");
        }

        var ahora = DateTimeOffset.UtcNow;
        var ping = new PingUbicacion
        {
            UniversidadId = viaje.UniversidadId,
            ViajeId = viaje.Id,
            UsuarioId = usuarioId,
            Lat = solicitud.Lat,
            Lng = solicitud.Lng,
            RegistradoEn = ahora
        };

        db.PingsUbicacion.Add(ping);
        await db.SaveChangesAsync(ct);

        return MapearPing(ping);
    }

    public async Task<TrackingViajeDto> ObtenerTrackingAsync(
        Guid usuarioId,
        Guid viajeId,
        CancellationToken ct = default)
    {
        var usuario = await db.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == usuarioId, ct)
            ?? throw ExcepcionTracking.NoEncontrado("User not found.", "user_not_found");

        if (usuario.Rol is RolUsuario.Coordinador or RolUsuario.SuperAdministrador)
        {
            throw ExcepcionTracking.Prohibido(
                "Coordinators must use GET /admin/tracking/active.",
                "use_admin_tracking");
        }

        var viaje = await db.Viajes
            .AsNoTracking()
            .Include(v => v.PuntosRuta)
            .FirstOrDefaultAsync(v => v.Id == viajeId, ct)
            ?? throw ExcepcionTracking.NoEncontrado("Trip not found.", "trip_not_found");

        if (viaje.Estado is not (EstadoViaje.Programado or EstadoViaje.EnCurso))
        {
            throw ExcepcionTracking.Conflicto(
                "Tracking is only available for scheduled or in-progress trips.",
                "trip_not_active");
        }

        var esConductor = viaje.ConductorId == usuarioId;
        var solicitudes = await db.SolicitudesViaje
            .AsNoTracking()
            .Where(s =>
                s.ViajeId == viaje.Id
                && (s.Estado == EstadoSolicitudViaje.Aceptada
                    || s.Estado == EstadoSolicitudViaje.Pendiente))
            .ToListAsync(ct);

        var aceptadas = solicitudes
            .Where(s => s.Estado == EstadoSolicitudViaje.Aceptada)
            .ToList();
        var pendientes = solicitudes
            .Where(s => s.Estado == EstadoSolicitudViaje.Pendiente)
            .ToList();

        var esPasajeroAceptado = aceptadas.Any(s => s.PasajeroId == usuarioId);
        var esPasajeroPendiente = pendientes.Any(s => s.PasajeroId == usuarioId);
        if (!esConductor && !esPasajeroAceptado && !esPasajeroPendiente)
        {
            throw ExcepcionTracking.Prohibido(
                "Only trip participants can view tracking.",
                "not_participant");
        }

        var ultimosPings = await ObtenerUltimosPingsAsync(viaje.Id, ct);
        var idsNombres = new List<Guid> { viaje.ConductorId };
        idsNombres.AddRange(aceptadas.Select(s => s.PasajeroId));
        idsNombres.AddRange(pendientes.Select(s => s.PasajeroId));
        var nombres = await ObtenerNombresAsync(idsNombres, ct);

        var participantes = new List<ParticipanteTrackingDto>();

        if (esConductor)
        {
            AgregarSiHayUbicacion(
                participantes,
                viaje.ConductorId,
                "driver",
                nombres,
                ultimosPings,
                pickup: null);

            foreach (var solicitud in aceptadas)
            {
                AgregarSiHayUbicacion(
                    participantes,
                    solicitud.PasajeroId,
                    "passenger",
                    nombres,
                    ultimosPings,
                    pickup: (solicitud.RecogidaLat, solicitud.RecogidaLng));
            }

            foreach (var solicitud in pendientes)
            {
                AgregarSiHayUbicacion(
                    participantes,
                    solicitud.PasajeroId,
                    "boarding",
                    nombres,
                    ultimosPings,
                    pickup: (solicitud.RecogidaLat, solicitud.RecogidaLng),
                    fuentePickup: "boarding");
            }
        }
        else
        {
            AgregarSiHayUbicacion(
                participantes,
                viaje.ConductorId,
                "driver",
                nombres,
                ultimosPings,
                pickup: null);

            AgregarSiHayUbicacion(
                participantes,
                usuarioId,
                esPasajeroPendiente ? "boarding" : "passenger",
                nombres,
                ultimosPings,
                pickup: null);

            var propia = solicitudes.FirstOrDefault(s => s.PasajeroId == usuarioId);
            if (propia is not null)
            {
                participantes.Add(new ParticipanteTrackingDto
                {
                    UsuarioId = usuarioId,
                    Rol = "boarding_point",
                    Nombre = "Punto de abordaje",
                    Lat = propia.RecogidaLat,
                    Lng = propia.RecogidaLng,
                    RegistradoEn = null,
                    Fuente = "boarding"
                });
            }
        }

        return new TrackingViajeDto
        {
            ViajeId = viaje.Id,
            Estado = ConversorEnumDominio.ACadenaDb(viaje.Estado),
            Polilinea = viaje.Polilinea,
            Waypoints = viaje.PuntosRuta
                .OrderBy(p => p.Seq)
                .Select(p => new TrackingWaypointDto
                {
                    Seq = p.Seq,
                    Lat = p.Lat,
                    Lng = p.Lng,
                    Etiqueta = p.Etiqueta
                })
                .ToList(),
            Participantes = participantes
        };
    }

    public async Task<TrackingAdminActivoDto> ListarActivosAdminAsync(CancellationToken ct = default)
    {
        if (inquilino.UniversidadId is null)
        {
            throw ExcepcionTracking.Prohibido(
                "University context required.",
                "missing_university_context");
        }

        var viajes = await db.Viajes
            .AsNoTracking()
            .Include(v => v.PuntosRuta)
            .Where(v => v.Estado == EstadoViaje.EnCurso)
            .OrderByDescending(v => v.IniciadoEn)
            .ToListAsync(ct);

        if (viajes.Count == 0)
        {
            return new TrackingAdminActivoDto { Viajes = [] };
        }

        var viajeIds = viajes.Select(v => v.Id).ToList();
        var solicitudes = await db.SolicitudesViaje
            .AsNoTracking()
            .Where(s => viajeIds.Contains(s.ViajeId) && s.Estado == EstadoSolicitudViaje.Aceptada)
            .ToListAsync(ct);

        var pings = await db.PingsUbicacion
            .AsNoTracking()
            .Where(p => viajeIds.Contains(p.ViajeId))
            .ToListAsync(ct);

        var ultimosPorViaje = pings
            .GroupBy(p => p.ViajeId)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(p => p.UsuarioId)
                    .ToDictionary(
                        ug => ug.Key,
                        ug => ug.OrderByDescending(p => p.RegistradoEn).First()));

        var usuarioIds = viajes.Select(v => v.ConductorId)
            .Concat(solicitudes.Select(s => s.PasajeroId))
            .Distinct()
            .ToList();
        var nombres = await ObtenerNombresAsync(usuarioIds, ct);

        var solicitudesPorViaje = solicitudes
            .GroupBy(s => s.ViajeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var resultado = new List<TrackingViajeDto>();
        foreach (var viaje in viajes)
        {
            ultimosPorViaje.TryGetValue(viaje.Id, out var ultimos);
            ultimos ??= new Dictionary<Guid, PingUbicacion>();

            solicitudesPorViaje.TryGetValue(viaje.Id, out var aceptadas);
            aceptadas ??= [];

            var participantes = new List<ParticipanteTrackingDto>();
            // Siempre incluir al conductor con nombre: ping en vivo o, si aún no
            // hay GPS, el origen del viaje (evita "Conductor sin nombre").
            AgregarSiHayUbicacion(
                participantes,
                viaje.ConductorId,
                "driver",
                nombres,
                ultimos,
                pickup: (viaje.OrigenLat, viaje.OrigenLng),
                fuentePickup: "origin");

            foreach (var solicitud in aceptadas)
            {
                AgregarSiHayUbicacion(
                    participantes,
                    solicitud.PasajeroId,
                    "passenger",
                    nombres,
                    ultimos,
                    pickup: (solicitud.RecogidaLat, solicitud.RecogidaLng));
            }

            resultado.Add(new TrackingViajeDto
            {
                ViajeId = viaje.Id,
                Estado = ConversorEnumDominio.ACadenaDb(viaje.Estado),
                Polilinea = viaje.Polilinea,
                Waypoints = viaje.PuntosRuta
                    .OrderBy(p => p.Seq)
                    .Select(p => new TrackingWaypointDto
                    {
                        Seq = p.Seq,
                        Lat = p.Lat,
                        Lng = p.Lng,
                        Etiqueta = p.Etiqueta
                    })
                    .ToList(),
                Participantes = participantes
            });
        }

        return new TrackingAdminActivoDto { Viajes = resultado };
    }

    private async Task<bool> EsParticipanteAsync(Viaje viaje, Guid usuarioId, CancellationToken ct)
    {
        if (viaje.ConductorId == usuarioId)
        {
            return true;
        }

        return await db.SolicitudesViaje.AnyAsync(
            s => s.ViajeId == viaje.Id
                 && s.PasajeroId == usuarioId
                 && (s.Estado == EstadoSolicitudViaje.Aceptada
                     || s.Estado == EstadoSolicitudViaje.Pendiente),
            ct);
    }

    private async Task<Dictionary<Guid, PingUbicacion>> ObtenerUltimosPingsAsync(
        Guid viajeId,
        CancellationToken ct)
    {
        var pings = await db.PingsUbicacion
            .AsNoTracking()
            .Where(p => p.ViajeId == viajeId)
            .ToListAsync(ct);

        return pings
            .GroupBy(p => p.UsuarioId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(p => p.RegistradoEn).First());
    }

    private async Task<Dictionary<Guid, string>> ObtenerNombresAsync(
        IEnumerable<Guid> usuarioIds,
        CancellationToken ct)
    {
        var ids = usuarioIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await db.Usuarios
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Nombre, ct);
    }

    private static void AgregarSiHayUbicacion(
        List<ParticipanteTrackingDto> destino,
        Guid usuarioId,
        string rol,
        Dictionary<Guid, string> nombres,
        Dictionary<Guid, PingUbicacion> ultimosPings,
        (double Lat, double Lng)? pickup,
        string fuentePickup = "pickup")
    {
        nombres.TryGetValue(usuarioId, out var nombreRaw);
        var nombre = string.IsNullOrWhiteSpace(nombreRaw)
            ? (rol == "driver" ? "Conductor" : "Pasajero")
            : nombreRaw.Trim();

        if (ultimosPings.TryGetValue(usuarioId, out var ping))
        {
            // Ubicación en vivo (conductor / pasajero).
            var rolVivo = rol == "boarding" ? "passenger" : rol;
            destino.Add(new ParticipanteTrackingDto
            {
                UsuarioId = usuarioId,
                Rol = rolVivo,
                Nombre = nombre,
                Lat = ping.Lat,
                Lng = ping.Lng,
                RegistradoEn = ping.RegistradoEn,
                Fuente = "ping"
            });

            // Punto de abordaje siempre visible para el conductor (pendiente o aceptado).
            if (pickup is { } abordaje && rol is "passenger" or "boarding")
            {
                destino.Add(new ParticipanteTrackingDto
                {
                    UsuarioId = usuarioId,
                    Rol = "boarding_point",
                    Nombre = $"{nombre} · abordaje",
                    Lat = abordaje.Lat,
                    Lng = abordaje.Lng,
                    RegistradoEn = null,
                    Fuente = "boarding"
                });
            }

            return;
        }

        if (pickup is { } punto)
        {
            // Sin ping: solo el punto de abordaje (no confundir con ubicación en vivo).
            if (rol is "passenger" or "boarding")
            {
                destino.Add(new ParticipanteTrackingDto
                {
                    UsuarioId = usuarioId,
                    Rol = "boarding_point",
                    Nombre = $"{nombre} · abordaje",
                    Lat = punto.Lat,
                    Lng = punto.Lng,
                    RegistradoEn = null,
                    Fuente = "boarding"
                });
                return;
            }

            destino.Add(new ParticipanteTrackingDto
            {
                UsuarioId = usuarioId,
                Rol = rol,
                Nombre = nombre,
                Lat = punto.Lat,
                Lng = punto.Lng,
                RegistradoEn = null,
                Fuente = fuentePickup
            });
        }
    }

    private static void ValidarCoordenadas(double lat, double lng)
    {
        if (lat is < -90 or > 90 || lng is < -180 or > 180)
        {
            throw ExcepcionTracking.Validacion(
                "lat must be in [-90, 90] and lng in [-180, 180].",
                "invalid_coordinates");
        }
    }

    private static PingDto MapearPing(PingUbicacion p) =>
        new()
        {
            Id = p.Id,
            ViajeId = p.ViajeId,
            UsuarioId = p.UsuarioId,
            Lat = p.Lat,
            Lng = p.Lng,
            RegistradoEn = p.RegistradoEn
        };
}
