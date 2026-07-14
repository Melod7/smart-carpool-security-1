namespace Kubix.Application.Viajes;

public interface IServicioSolicitudesViaje
{
    Task<IReadOnlyList<ViajeDto>> ListarDisponiblesAsync(
        Guid usuarioId,
        double? lat = null,
        double? lng = null,
        CancellationToken ct = default);

    Task<PuntoEsperaSugeridoDto> ObtenerPuntoEsperaSugeridoAsync(
        Guid usuarioId,
        Guid viajeId,
        double lat,
        double lng,
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
