namespace Kubix.Application.Auth;

public interface IServicioAutenticacion
{
    Task<RespuestaAutenticacion> IniciarSesionAsync(SolicitudInicioSesion solicitud, CancellationToken ct = default);
    Task<RespuestaAutenticacion> RefrescarAsync(SolicitudRefresco solicitud, CancellationToken ct = default);
    Task CerrarSesionAsync(Guid usuarioId, SolicitudCierreSesion solicitud, CancellationToken ct = default);
    Task CambiarContrasenaAsync(Guid usuarioId, SolicitudCambioContrasena solicitud, CancellationToken ct = default);
    Task<ResumenUsuarioDto> ObtenerPerfilAsync(Guid usuarioId, CancellationToken ct = default);
}
