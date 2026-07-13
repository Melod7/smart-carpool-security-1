namespace Kubix.Application.Calificaciones;

public interface IServicioCalificaciones
{
    Task<CalificacionDto> CrearAsync(
        Guid calificadorId,
        Guid viajeId,
        SolicitudCrearCalificacion solicitud,
        CancellationToken ct = default);

    Task<IReadOnlyList<CalificacionPendienteDto>> ListarPendientesAsync(
        Guid usuarioId,
        CancellationToken ct = default);
}
