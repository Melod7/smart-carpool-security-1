namespace Kubix.Application.Auth;

public interface ICacheEstadoUsuario
{
    Task<EstadoSesionUsuario> ObtenerAsync(Guid usuarioId, CancellationToken ct = default);
    void Invalidar(Guid usuarioId);
    Task InvalidarUniversidadAsync(Guid universidadId, CancellationToken ct = default);
}
