namespace Kubix.Application.Viajes;

public interface IServicioViajes
{
    Task<VehiculoDto> ObtenerVehiculoAsync(Guid usuarioId, CancellationToken ct = default);

    Task<VehiculoDto> UpsertVehiculoAsync(
        Guid usuarioId,
        SolicitudUpsertVehiculo solicitud,
        CancellationToken ct = default);

    Task<ViajeDto> PublicarViajeAsync(
        Guid usuarioId,
        SolicitudPublicarViaje solicitud,
        CancellationToken ct = default);

    Task<ViajeDto> IniciarViajeAsync(
        Guid usuarioId,
        Guid viajeId,
        CancellationToken ct = default);

    Task<ViajeDto> CompletarViajeAsync(
        Guid usuarioId,
        Guid viajeId,
        CancellationToken ct = default);

    Task<ViajeDto> CancelarViajeAsync(
        Guid usuarioId,
        Guid viajeId,
        CancellationToken ct = default);

    Task<MisViajesDto> ListarMisViajesAsync(
        Guid usuarioId,
        string? periodo = null,
        CancellationToken ct = default);
}
