namespace Kubix.Application.Viajes;

public interface IServicioSolicitudesViaje
{
    Task<IReadOnlyList<ViajeDto>> ListarDisponiblesAsync(
        Guid usuarioId,
        CancellationToken ct = default);

    Task<SolicitudViajeDto> CrearSolicitudAsync(
        Guid usuarioId,
        Guid viajeId,
        SolicitudCrearSolicitudViaje solicitud,
        CancellationToken ct = default);

    Task<IReadOnlyList<SolicitudViajeDto>> ListarPorViajeAsync(
        Guid usuarioId,
        Guid viajeId,
        CancellationToken ct = default);

    Task<SolicitudViajeDto> AceptarAsync(
        Guid usuarioId,
        Guid solicitudId,
        CancellationToken ct = default);

    Task<SolicitudViajeDto> RechazarAsync(
        Guid usuarioId,
        Guid solicitudId,
        CancellationToken ct = default);

    Task<SolicitudViajeDto> CancelarAsync(
        Guid usuarioId,
        Guid solicitudId,
        CancellationToken ct = default);
}
