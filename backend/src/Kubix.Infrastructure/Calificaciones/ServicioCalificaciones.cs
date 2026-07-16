using Kubix.Application.Auth;
using Kubix.Application.Calificaciones;
using Kubix.Application.EcoTokens;
using Kubix.Application.Tenancy;
using Kubix.Domain.Entities;
using Kubix.Domain.Enums;
using Kubix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kubix.Infrastructure.Calificaciones;

public sealed class ServicioCalificaciones(
    ContextoApp db,
    IEscritorAuditoria auditoria,
    IMotorEcoTokens motorEcoTokens,
    ICacheEstadoUsuario cacheEstado) : IServicioCalificaciones
{
    private static readonly TimeSpan VentanaCalificacionConductor = TimeSpan.FromHours(12);

    public async Task<CalificacionDto> CrearAsync(
        Guid calificadorId,
        Guid viajeId,
        SolicitudCrearCalificacion solicitud,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (solicitud.Estrellas is < 1 or > 5)
        {
            throw ExcepcionCalificaciones.Validacion(
                "Stars must be between 1 and 5.",
                "invalid_stars");
        }

        if (solicitud.CalificadoId == Guid.Empty)
        {
            throw ExcepcionCalificaciones.Validacion(
                "ratedUserId is required.",
                "rated_user_required");
        }

        if (solicitud.CalificadoId == calificadorId)
        {
            throw ExcepcionCalificaciones.Validacion(
                "Cannot rate yourself.",
                "cannot_rate_self");
        }

        var viaje = await db.Viajes
            .Include(v => v.SolicitudesViaje)
            .FirstOrDefaultAsync(v => v.Id == viajeId, ct)
            ?? throw ExcepcionCalificaciones.NoEncontrado("Trip not found.", "trip_not_found");

        if (viaje.Estado != EstadoViaje.Completado)
        {
            throw ExcepcionCalificaciones.Conflicto(
                "Trip must be completed before rating.",
                "trip_not_completed");
        }

        var esConductor = viaje.ConductorId == calificadorId;
        var solicitudAceptadaRater = viaje.SolicitudesViaje.FirstOrDefault(s =>
            s.PasajeroId == calificadorId && s.Estado == EstadoSolicitudViaje.Aceptada);

        if (!esConductor && solicitudAceptadaRater is null)
        {
            throw ExcepcionCalificaciones.Prohibido(
                "Only trip participants can submit ratings.",
                "not_participant");
        }

        if (esConductor)
        {
            var pasajeroAceptado = viaje.SolicitudesViaje.Any(s =>
                s.PasajeroId == solicitud.CalificadoId && s.Estado == EstadoSolicitudViaje.Aceptada);
            if (!pasajeroAceptado)
            {
                throw ExcepcionCalificaciones.Validacion(
                    "Driver can only rate accepted passengers of this trip.",
                    "invalid_rated_user");
            }

            var completadoEn = viaje.CompletadoEn
                ?? throw ExcepcionCalificaciones.Validacion(
                    "Trip completion timestamp is missing.",
                    "missing_completed_at");

            if (DateTimeOffset.UtcNow > completadoEn.Add(VentanaCalificacionConductor))
            {
                throw ExcepcionCalificaciones.Validacion(
                    "Driver rating window of 12 hours has expired.",
                    "rating_window_expired");
            }
        }
        else
        {
            if (solicitud.CalificadoId != viaje.ConductorId)
            {
                throw ExcepcionCalificaciones.Validacion(
                    "Passenger can only rate the trip driver.",
                    "invalid_rated_user");
            }
        }

        var duplicada = await db.Calificaciones.AnyAsync(
            c => c.ViajeId == viajeId
                 && c.CalificadorId == calificadorId
                 && c.CalificadoId == solicitud.CalificadoId,
            ct);
        if (duplicada)
        {
            throw ExcepcionCalificaciones.Conflicto(
                "Rating already submitted for this trip and user.",
                "rating_already_exists");
        }

        var comentario = string.IsNullOrWhiteSpace(solicitud.Comentario)
            ? null
            : solicitud.Comentario.Trim();
        if (comentario is { Length: > 1000 })
        {
            throw ExcepcionCalificaciones.Validacion(
                "Comment must be at most 1000 characters.",
                "comment_too_long");
        }

        var ahora = DateTimeOffset.UtcNow;
        var calificacion = new Calificacion
        {
            UniversidadId = viaje.UniversidadId,
            ViajeId = viaje.Id,
            CalificadorId = calificadorId,
            CalificadoId = solicitud.CalificadoId,
            Estrellas = solicitud.Estrellas,
            Comentario = comentario,
            CreadoEn = ahora,
            ActualizadoEn = ahora
        };

        db.Calificaciones.Add(calificacion);
        await db.SaveChangesAsync(ct);

        await RecomputarYAplicarEnforcementAsync(solicitud.CalificadoId, ct);
        await motorEcoTokens.AlCalificarAsync(calificacion.Id, ct);

        return Mapear(calificacion);
    }

    public async Task<IReadOnlyList<CalificacionPendienteDto>> ListarPendientesAsync(
        Guid usuarioId,
        CancellationToken ct = default)
    {
        var usuario = await db.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == usuarioId, ct)
            ?? throw ExcepcionCalificaciones.NoEncontrado("User not found.");

        var ahora = DateTimeOffset.UtcNow;
        var resultado = new List<CalificacionPendienteDto>();

        if (usuario.Rol == RolUsuario.Pasajero)
        {
            var pendientes = await db.SolicitudesViaje
                .AsNoTracking()
                .Where(s =>
                    s.PasajeroId == usuarioId
                    && s.Estado == EstadoSolicitudViaje.Aceptada
                    && s.Viaje.Estado == EstadoViaje.Completado)
                .Where(s => !db.Calificaciones.Any(c =>
                    c.ViajeId == s.ViajeId
                    && c.CalificadorId == usuarioId
                    && c.CalificadoId == s.Viaje.ConductorId))
                .Select(s => new
                {
                    s.ViajeId,
                    CalificadoId = s.Viaje.ConductorId,
                    Nombre = s.Viaje.Conductor.Nombre
                })
                .ToListAsync(ct);

            foreach (var item in pendientes)
            {
                resultado.Add(new CalificacionPendienteDto
                {
                    ViajeId = item.ViajeId,
                    CalificadoId = item.CalificadoId,
                    NombreCalificado = item.Nombre,
                    RolACalificar = "driver",
                    ExpiraEn = null
                });
            }
        }
        else if (usuario.Rol == RolUsuario.Conductor)
        {
            var limite = ahora - VentanaCalificacionConductor;
            var pendientes = await db.SolicitudesViaje
                .AsNoTracking()
                .Where(s =>
                    s.Viaje.ConductorId == usuarioId
                    && s.Estado == EstadoSolicitudViaje.Aceptada
                    && s.Viaje.Estado == EstadoViaje.Completado
                    && s.Viaje.CompletadoEn != null
                    && s.Viaje.CompletadoEn >= limite)
                .Where(s => !db.Calificaciones.Any(c =>
                    c.ViajeId == s.ViajeId
                    && c.CalificadorId == usuarioId
                    && c.CalificadoId == s.PasajeroId))
                .Select(s => new
                {
                    s.ViajeId,
                    CalificadoId = s.PasajeroId,
                    Nombre = s.Pasajero.Nombre,
                    CompletadoEn = s.Viaje.CompletadoEn!.Value
                })
                .ToListAsync(ct);

            foreach (var item in pendientes)
            {
                resultado.Add(new CalificacionPendienteDto
                {
                    ViajeId = item.ViajeId,
                    CalificadoId = item.CalificadoId,
                    NombreCalificado = item.Nombre,
                    RolACalificar = "passenger",
                    ExpiraEn = item.CompletadoEn.Add(VentanaCalificacionConductor)
                });
            }
        }

        return resultado
            .OrderBy(p => p.ExpiraEn.HasValue ? 0 : 1)
            .ThenBy(p => p.ExpiraEn)
            .ThenBy(p => p.ViajeId)
            .ToList();
    }

    private async Task RecomputarYAplicarEnforcementAsync(Guid calificadoId, CancellationToken ct)
    {
        var calificado = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == calificadoId, ct)
            ?? throw ExcepcionCalificaciones.NoEncontrado("Rated user not found.");

        var estrellas = await db.Calificaciones
            .Where(c => c.CalificadoId == calificadoId)
            .Select(c => c.Estrellas)
            .ToListAsync(ct);

        var cantidad = estrellas.Count;
        var promedio = cantidad == 0
            ? 0m
            : Math.Round((decimal)estrellas.Average(), 2, MidpointRounding.AwayFromZero);

        calificado.PromedioCalificacion = promedio;
        calificado.ActualizadoEn = DateTimeOffset.UtcNow;

        var debeBloquear = calificado.Rol == RolUsuario.Conductor
            && calificado.Estado != EstadoUsuario.Bloqueado
            && calificado.Estado != EstadoUsuario.Eliminado
            && calificado.UniversidadId.HasValue
            && cantidad >= 5;

        if (debeBloquear)
        {
            var config = await db.ConfiguracionesUniversidad
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UniversidadId == calificado.UniversidadId!.Value, ct);

            if (config is not null && promedio < config.CalificacionMinimaConductor)
            {
                calificado.Estado = EstadoUsuario.Bloqueado;

                var ahora = DateTimeOffset.UtcNow;
                var tokens = await db.TokensRefresco
                    .Where(t => t.UsuarioId == calificadoId && t.RevocadoEn == null)
                    .ToListAsync(ct);
                foreach (var token in tokens)
                {
                    token.RevocadoEn = ahora;
                }

                db.Notificaciones.Add(new Notificacion
                {
                    UniversidadId = calificado.UniversidadId!.Value,
                    RolDestinatario = RolUsuario.Coordinador,
                    Tipo = TipoNotificacion.Bloqueo,
                    Titulo = "Conductor bloqueado por calificación",
                    Cuerpo =
                        $"{calificado.Nombre} fue bloqueado automáticamente: promedio {promedio:0.00} " +
                        $"menor a {config.CalificacionMinimaConductor:0.00} ({cantidad} calificaciones).",
                    CreadoEn = ahora
                });

                await db.SaveChangesAsync(ct);
                cacheEstado.Invalidar(calificadoId);

                await auditoria.EscribirAsync(
                    $"user.auto_blocked_min_rating:{calificadoId}",
                    TipoEventoAuditoria.Sistema,
                    SeveridadAuditoria.Alta,
                    universidadId: calificado.UniversidadId,
                    usuarioId: calificadoId,
                    ct: ct);
                return;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static CalificacionDto Mapear(Calificacion c) =>
        new()
        {
            Id = c.Id,
            ViajeId = c.ViajeId,
            CalificadorId = c.CalificadorId,
            CalificadoId = c.CalificadoId,
            Estrellas = c.Estrellas,
            Comentario = c.Comentario,
            UniversidadId = c.UniversidadId
        };
}
