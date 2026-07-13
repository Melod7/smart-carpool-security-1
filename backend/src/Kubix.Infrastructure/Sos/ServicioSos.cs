using Kubix.Application.Sos;
using Kubix.Application.Tenancy;
using Kubix.Domain;
using Kubix.Domain.Entities;
using Kubix.Domain.Enums;
using Kubix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kubix.Infrastructure.Sos;

public sealed class ServicioSos(
    ContextoApp db,
    IEscritorAuditoria auditoria,
    IContextoInquilino inquilino) : IServicioSos
{
    public async Task<AlertaSosDto> CrearAsync(
        Guid usuarioId,
        SolicitudCrearSos solicitud,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ValidarCoordenadas(solicitud.Lat, solicitud.Lng);

        var usuario = await db.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == usuarioId, ct)
            ?? throw ExcepcionSos.NoEncontrado("User not found.", "user_not_found");

        if (usuario.UniversidadId is not Guid universidadId)
        {
            throw ExcepcionSos.Validacion("User has no university.", "missing_university");
        }

        Guid? viajeId = null;
        if (solicitud.ViajeId is Guid idViaje)
        {
            var viaje = await db.Viajes
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == idViaje, ct)
                ?? throw ExcepcionSos.NoEncontrado("Trip not found.", "trip_not_found");

            if (viaje.UniversidadId != universidadId)
            {
                throw ExcepcionSos.NoEncontrado("Trip not found.", "trip_not_found");
            }

            viajeId = viaje.Id;
        }

        var ahora = DateTimeOffset.UtcNow;
        var alerta = new AlertaSos
        {
            UniversidadId = universidadId,
            ViajeId = viajeId,
            UsuarioId = usuarioId,
            Lat = solicitud.Lat,
            Lng = solicitud.Lng,
            DisparadaEn = ahora,
            Estado = EstadoAlertaSos.Activa,
            CreadoEn = ahora,
            ActualizadoEn = ahora
        };

        db.AlertasSos.Add(alerta);

        var config = await db.ConfiguracionesUniversidad
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UniversidadId == universidadId, ct);

        if (config is null || config.NotificarSos)
        {
            db.Notificaciones.Add(new Notificacion
            {
                UniversidadId = universidadId,
                RolDestinatario = RolUsuario.Coordinador,
                Tipo = TipoNotificacion.Sos,
                Titulo = "Alerta SOS activa",
                Cuerpo = $"{usuario.Nombre} disparó una alerta SOS",
                CreadoEn = ahora
            });
        }

        await db.SaveChangesAsync(ct);

        await auditoria.EscribirAsync(
            "sos.fired",
            TipoEventoAuditoria.Sos,
            SeveridadAuditoria.Alta,
            universidadId: universidadId,
            usuarioId: usuarioId,
            ct: ct);

        return Mapear(alerta);
    }

    public async Task<AlertaSosDto> CerrarAsync(
        Guid usuarioId,
        Guid alertaId,
        CancellationToken ct = default)
    {
        var alerta = await db.AlertasSos.FirstOrDefaultAsync(a => a.Id == alertaId, ct);
        if (alerta is null || alerta.UsuarioId != usuarioId)
        {
            throw ExcepcionSos.NoEncontrado("SOS alert not found.", "sos_not_found");
        }

        if (alerta.Estado == EstadoAlertaSos.Resuelta)
        {
            throw ExcepcionSos.Conflicto("SOS alert is already resolved.", "sos_already_resolved");
        }

        var ahora = DateTimeOffset.UtcNow;
        alerta.Estado = EstadoAlertaSos.Resuelta;
        alerta.ResueltaPor = usuarioId;
        alerta.ResueltaEn = ahora;
        alerta.ActualizadoEn = ahora;
        await db.SaveChangesAsync(ct);

        await auditoria.EscribirAsync(
            "sos.closed",
            TipoEventoAuditoria.Sos,
            SeveridadAuditoria.Alta,
            universidadId: alerta.UniversidadId,
            usuarioId: usuarioId,
            ct: ct);

        return Mapear(alerta);
    }

    public async Task<IReadOnlyList<AlertaSosAdminDto>> ListarAdminAsync(CancellationToken ct = default)
    {
        if (inquilino.UniversidadId is null)
        {
            throw ExcepcionSos.Prohibido("University context required.", "missing_university_context");
        }

        var alertas = await db.AlertasSos
            .AsNoTracking()
            .Include(a => a.Usuario)
            .Include(a => a.Viaje!)
                .ThenInclude(v => v.Conductor)
            .Where(a =>
                a.Estado == EstadoAlertaSos.Activa || a.Estado == EstadoAlertaSos.Resuelta)
            .OrderByDescending(a => a.DisparadaEn)
            .ToListAsync(ct);

        return alertas.Select(MapearAdmin).ToList();
    }

    public async Task<AlertaSosDto> ResolverAdminAsync(
        Guid coordinadorId,
        Guid alertaId,
        CancellationToken ct = default)
    {
        var alerta = await db.AlertasSos.FirstOrDefaultAsync(a => a.Id == alertaId, ct)
            ?? throw ExcepcionSos.NoEncontrado("SOS alert not found.", "sos_not_found");

        if (alerta.Estado == EstadoAlertaSos.Resuelta)
        {
            throw ExcepcionSos.Conflicto("SOS alert is already resolved.", "sos_already_resolved");
        }

        var ahora = DateTimeOffset.UtcNow;
        alerta.Estado = EstadoAlertaSos.Resuelta;
        alerta.ResueltaPor = coordinadorId;
        alerta.ResueltaEn = ahora;
        alerta.ActualizadoEn = ahora;
        await db.SaveChangesAsync(ct);

        await auditoria.EscribirAsync(
            "sos.resolved",
            TipoEventoAuditoria.Sos,
            SeveridadAuditoria.Alta,
            universidadId: alerta.UniversidadId,
            usuarioId: coordinadorId,
            ct: ct);

        return Mapear(alerta);
    }

    private static void ValidarCoordenadas(double lat, double lng)
    {
        if (lat is < -90 or > 90 || lng is < -180 or > 180)
        {
            throw ExcepcionSos.Validacion(
                "lat must be in [-90, 90] and lng in [-180, 180].",
                "invalid_coordinates");
        }
    }

    private static AlertaSosDto Mapear(AlertaSos a) =>
        new()
        {
            Id = a.Id,
            Estado = ConversorEnumDominio.ACadenaDb(a.Estado),
            Lat = a.Lat,
            Lng = a.Lng,
            ViajeId = a.ViajeId,
            DisparadaEn = a.DisparadaEn,
            ResueltaPor = a.ResueltaPor,
            ResueltaEn = a.ResueltaEn,
            UniversidadId = a.UniversidadId,
            UsuarioId = a.UsuarioId
        };

    private static AlertaSosAdminDto MapearAdmin(AlertaSos a)
    {
        ParticipanteSosDto? conductor = null;
        ViajeSosDto? viaje = null;

        if (a.Viaje is not null)
        {
            viaje = new ViajeSosDto
            {
                Id = a.Viaje.Id,
                Estado = ConversorEnumDominio.ACadenaDb(a.Viaje.Estado),
                OrigenTexto = a.Viaje.OrigenTexto,
                SaleEn = a.Viaje.SaleEn
            };

            if (a.Viaje.Conductor is not null)
            {
                conductor = MapearParticipante(a.Viaje.Conductor);
            }
        }

        return new AlertaSosAdminDto
        {
            Id = a.Id,
            Estado = ConversorEnumDominio.ACadenaDb(a.Estado),
            Lat = a.Lat,
            Lng = a.Lng,
            DisparadaEn = a.DisparadaEn,
            ResueltaPor = a.ResueltaPor,
            ResueltaEn = a.ResueltaEn,
            Estudiante = MapearParticipante(a.Usuario),
            Viaje = viaje,
            Conductor = conductor
        };
    }

    private static ParticipanteSosDto MapearParticipante(Usuario u) =>
        new()
        {
            Id = u.Id,
            Nombre = u.Nombre,
            Correo = u.Correo,
            Rol = ConversorEnumDominio.ACadenaDb(u.Rol)
        };
}
