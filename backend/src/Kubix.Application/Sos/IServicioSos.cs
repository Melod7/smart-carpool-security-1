namespace Kubix.Application.Sos;

public interface IServicioSos
{
    Task<AlertaSosDto> CrearAsync(
        Guid usuarioId,
        SolicitudCrearSos solicitud,
        CancellationToken ct = default);

    Task<AlertaSosDto> CerrarAsync(
        Guid usuarioId,
        Guid alertaId,
        CancellationToken ct = default);

    Task<IReadOnlyList<AlertaSosAdminDto>> ListarAdminAsync(CancellationToken ct = default);

    Task<AlertaSosDto> ResolverAdminAsync(
        Guid coordinadorId,
        Guid alertaId,
        CancellationToken ct = default);
}
