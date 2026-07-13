namespace Kubix.Application.Admin;

public interface IServicioAdminOps
{
    Task<DashboardAdminDto> ObtenerDashboardAsync(CancellationToken ct = default);

    Task<ReporteAdminDto> ObtenerReporteAsync(string periodo, CancellationToken ct = default);

    Task<ArchivoExportacion> ExportarReporteAsync(
        string periodo,
        string formato,
        CancellationToken ct = default);

    Task<PaginaAuditoriaDto> ListarAuditoriaAsync(
        string? tipo,
        string? severidad,
        int pagina,
        int tamanoPagina,
        CancellationToken ct = default);

    Task<IReadOnlyList<NotificacionAdminDto>> ListarNotificacionesAsync(
        Guid usuarioId,
        CancellationToken ct = default);

    Task MarcarNotificacionLeidaAsync(Guid usuarioId, Guid notificacionId, CancellationToken ct = default);

    Task MarcarTodasNotificacionesLeidasAsync(Guid usuarioId, CancellationToken ct = default);

    Task<ConfiguracionUniversidadDto> ObtenerConfiguracionAsync(CancellationToken ct = default);

    Task<ConfiguracionUniversidadDto> ActualizarConfiguracionAsync(
        SolicitudActualizarConfiguracion solicitud,
        CancellationToken ct = default);
}
