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

        if (viaje.Estado != EstadoViaje.EnCurso)
        {
            throw ExcepcionTracking.Conflicto(
                "Pings are only allowed on in-progress trips.",
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
            .FirstOrDefaultAsync(v => v.Id == viajeId, ct)
            ?? throw ExcepcionTracking.NoEncontrado("Trip not found.", "trip_not_found");

        if (viaje.Estado != EstadoViaje.EnCurso)
        {
            throw ExcepcionTracking.Conflicto(
                "Tracking is only available for in-progress trips.",
                "trip_not_active");
        }

        var esConductor = viaje.ConductorId == usuarioId;
        var solicitudesAceptadas = await db.SolicitudesViaje
            .AsNoTracking()
            .Where(s => s.ViajeId == viaje.Id && s.Estado == EstadoSolicitudViaje.Aceptada)
            .ToListAsync(ct);

        var esPasajero = solicitudesAceptadas.Any(s => s.PasajeroId == usuarioId);
        if (!esConductor && !esPasajero)
        {
            throw ExcepcionTracking.Prohibido(
                "Only trip participants can view tracking.",
                "not_participant");
        }

        var ultimosPings = await ObtenerUltimosPingsAsync(viaje.Id, ct);
        var nombres = await ObtenerNombresAsync(
            [viaje.ConductorId, .. solicitudesAceptadas.Select(s => s.PasajeroId)],
            ct);

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

            foreach (var solicitud in solicitudesAceptadas)
            {
                AgregarSiHayUbicacion(
                    participantes,
                    solicitud.PasajeroId,
                    "passenger",
                    nombres,
                    ultimosPings,
                    pickup: (solicitud.RecogidaLat, solicitud.RecogidaLng));
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
                "passenger",
                nombres,
                ultimosPings,
                pickup: null);
        }

        return new TrackingViajeDto
        {
            ViajeId = viaje.Id,
            Estado = ConversorEnumDominio.ACadenaDb(viaje.Estado),
            Polilinea = viaje.Polilinea,
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
            AgregarSiHayUbicacion(
                participantes,
                viaje.ConductorId,
                "driver",
                nombres,
                ultimos,
                pickup: null);

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
                 && s.Estado == EstadoSolicitudViaje.Aceptada,
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
        (double Lat, double Lng)? pickup)
    {
        nombres.TryGetValue(usuarioId, out var nombre);

        if (ultimosPings.TryGetValue(usuarioId, out var ping))
        {
            destino.Add(new ParticipanteTrackingDto
            {
                UsuarioId = usuarioId,
                Rol = rol,
                Nombre = nombre,
                Lat = ping.Lat,
                Lng = ping.Lng,
                RegistradoEn = ping.RegistradoEn,
                Fuente = "ping"
            });
            return;
        }

        if (pickup is { } punto)
        {
            destino.Add(new ParticipanteTrackingDto
            {
                UsuarioId = usuarioId,
                Rol = rol,
                Nombre = nombre,
                Lat = punto.Lat,
                Lng = punto.Lng,
                RegistradoEn = null,
                Fuente = "pickup"
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
