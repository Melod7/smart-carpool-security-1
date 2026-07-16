namespace Kubix.Application.Usuarios;

public interface IServicioRegistroUsuarios
{
    Task<RespuestaRegistroDto> RegistrarAsync(SolicitudRegistroDto solicitud, CancellationToken ct = default);

    Task<IReadOnlyList<UniversidadPublicaDto>> ListarUniversidadesPublicasAsync(CancellationToken ct = default);

    Task<IReadOnlyList<SolicitudRegistroResumenDto>> ListarSolicitudesPendientesAsync(CancellationToken ct = default);

    Task AceptarSolicitudAsync(Guid solicitudId, CancellationToken ct = default);

    Task DenegarSolicitudAsync(Guid solicitudId, CancellationToken ct = default);

    Task<PaginaUsuariosAdmin> ListarUsuariosAsync(FiltroUsuariosAdmin filtro, CancellationToken ct = default);

    Task BloquearUsuarioAsync(Guid usuarioId, CancellationToken ct = default);

    Task DesbloquearUsuarioAsync(Guid usuarioId, CancellationToken ct = default);

    Task EliminarUsuarioAsync(Guid usuarioId, CancellationToken ct = default);

    Task<string> ExportarUsuariosCsvAsync(FiltroUsuariosAdmin filtro, CancellationToken ct = default);

    Task<PerfilUsuarioDto> ObtenerPerfilAsync(Guid usuarioId, CancellationToken ct = default);

    Task<PerfilUsuarioDto> ActualizarPerfilAsync(
        Guid usuarioId,
        SolicitudActualizarPerfil solicitud,
        CancellationToken ct = default);

    Task<RespuestaCambioPerfilDto> SolicitarCambioPerfilAsync(
        Guid usuarioId,
        SolicitudActualizarPerfil solicitud,
        CancellationToken ct = default);

    Task<RespuestaCambioModoDto> CambiarModoAsync(
        Guid usuarioId,
        SolicitudCambioModo solicitud,
        CancellationToken ct = default);

    Task<IReadOnlyList<ContactoEmergenciaDto>> ListarContactosEmergenciaAsync(
        Guid usuarioId,
        CancellationToken ct = default);

    Task<ContactoEmergenciaDto> CrearContactoEmergenciaAsync(
        Guid usuarioId,
        SolicitudCrearContactoEmergencia solicitud,
        CancellationToken ct = default);

    Task EliminarContactoEmergenciaAsync(Guid usuarioId, Guid contactoId, CancellationToken ct = default);
}
