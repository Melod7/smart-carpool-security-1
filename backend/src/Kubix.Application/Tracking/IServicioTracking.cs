namespace Kubix.Application.Tracking;

public interface IServicioTracking
{
    Task<PingDto> RegistrarPingAsync(
        Guid usuarioId,
        Guid viajeId,
        SolicitudPing solicitud,
        CancellationToken ct = default);

    Task<TrackingViajeDto> ObtenerTrackingAsync(
        Guid usuarioId,
        Guid viajeId,
        CancellationToken ct = default);

    Task<TrackingAdminActivoDto> ListarActivosAdminAsync(CancellationToken ct = default);
}
