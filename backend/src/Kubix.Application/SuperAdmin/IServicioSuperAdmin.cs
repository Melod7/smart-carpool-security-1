namespace Kubix.Application.SuperAdmin;

public interface IServicioSuperAdmin
{
    Task<IReadOnlyList<UniversidadResumenDto>> ListarUniversidadesAsync(CancellationToken ct = default);

    Task<UniversidadDetalleDto> CrearUniversidadAsync(
        SolicitudCrearUniversidad solicitud,
        CancellationToken ct = default);

    Task<UniversidadDetalleDto> ActualizarUniversidadAsync(
        Guid id,
        SolicitudActualizarUniversidad solicitud,
        CancellationToken ct = default);

    Task SuspenderUniversidadAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<CampusDto>> ListarCampusesAsync(Guid universidadId, CancellationToken ct = default);

    Task<CampusDto> CrearCampusAsync(
        Guid universidadId,
        SolicitudCrearCampus solicitud,
        CancellationToken ct = default);

    Task<CampusDto> ActualizarCampusAsync(
        Guid universidadId,
        Guid campusId,
        SolicitudActualizarCampus solicitud,
        CancellationToken ct = default);

    Task EliminarCampusAsync(Guid universidadId, Guid campusId, CancellationToken ct = default);

    Task<IReadOnlyList<CoordinadorDto>> ListarCoordinadoresAsync(
        Guid universidadId,
        CancellationToken ct = default);

    Task<CoordinadorCreadoDto> CrearCoordinadorAsync(
        Guid universidadId,
        SolicitudCrearCoordinador solicitud,
        CancellationToken ct = default);

    Task<RespuestaResetContrasena> ResetearContrasenaCoordinadorAsync(
        Guid coordinadorId,
        CancellationToken ct = default);

    Task<StatsSuperAdminDto> ObtenerStatsAsync(CancellationToken ct = default);
}
