using System.Security.Claims;
using Kubix.Application.Admin;
using Kubix.Application.Auth;
using Kubix.Application.EcoTokens;
using Kubix.Application.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kubix.Api.Controladores;

[ApiController]
[Authorize(Policy = NombresPoliticas.SoloCoordinador)]
[Route("admin")]
public sealed class ControladorAdminOps(
    IServicioAdminOps admin,
    IServicioEcoTokens ecoTokens) : ControllerBase
{
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(DashboardAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        try
        {
            return Ok(await admin.ObtenerDashboardAsync(ct));
        }
        catch (ExcepcionAdminOps ex)
        {
            return ProblemFrom(ex);
        }
    }

    [HttpGet("reports")]
    [ProducesResponseType(typeof(ReporteAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Reportes(
        [FromQuery(Name = "period")] string? periodo,
        CancellationToken ct)
    {
        try
        {
            return Ok(await admin.ObtenerReporteAsync(periodo ?? "semanal", ct));
        }
        catch (ExcepcionAdminOps ex)
        {
            return ProblemFrom(ex);
        }
    }

    [HttpGet("reports/export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExportarReporte(
        [FromQuery(Name = "period")] string? periodo,
        [FromQuery(Name = "format")] string? formato,
        CancellationToken ct)
    {
        try
        {
            var archivo = await admin.ExportarReporteAsync(
                periodo ?? "semanal",
                formato ?? "csv",
                ct);
            return File(archivo.Contenido, archivo.ContentType, archivo.NombreArchivo);
        }
        catch (ExcepcionAdminOps ex)
        {
            return ProblemFrom(ex);
        }
    }

    [HttpGet("audit-log")]
    [ProducesResponseType(typeof(PaginaAuditoriaDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Auditoria(
        [FromQuery(Name = "type")] string? tipo,
        [FromQuery(Name = "severity")] string? severidad,
        [FromQuery(Name = "page")] int pagina = 1,
        [FromQuery(Name = "pageSize")] int tamanoPagina = 20,
        CancellationToken ct = default)
    {
        try
        {
            return Ok(await admin.ListarAuditoriaAsync(tipo, severidad, pagina, tamanoPagina, ct));
        }
        catch (ExcepcionAdminOps ex)
        {
            return ProblemFrom(ex);
        }
        catch (ArgumentOutOfRangeException)
        {
            return ProblemFrom(ExcepcionAdminOps.Validacion(
                "Invalid type or severity filter.",
                "invalid_filter"));
        }
    }

    [HttpGet("audit-log/export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExportarAuditoria(
        [FromQuery(Name = "type")] string? tipo,
        [FromQuery(Name = "severity")] string? severidad,
        CancellationToken ct)
    {
        try
        {
            var archivo = await admin.ExportarAuditoriaPdfAsync(tipo, severidad, ct);
            return File(archivo.Contenido, archivo.ContentType, archivo.NombreArchivo);
        }
        catch (ExcepcionAdminOps ex)
        {
            return ProblemFrom(ex);
        }
    }

    [HttpGet("notifications")]
    [ProducesResponseType(typeof(IReadOnlyList<NotificacionAdminDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Notificaciones(CancellationToken ct)
    {
        try
        {
            return Ok(await admin.ListarNotificacionesAsync(ObtenerUsuarioId(), ct));
        }
        catch (ExcepcionAdminOps ex)
        {
            return ProblemFrom(ex);
        }
        catch (ExcepcionAutenticacion ex)
        {
            return ProblemAuth(ex);
        }
    }

    [HttpPut("notifications/{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarcarLeida(Guid id, CancellationToken ct)
    {
        try
        {
            await admin.MarcarNotificacionLeidaAsync(ObtenerUsuarioId(), id, ct);
            return NoContent();
        }
        catch (ExcepcionAdminOps ex)
        {
            return ProblemFrom(ex);
        }
        catch (ExcepcionAutenticacion ex)
        {
            return ProblemAuth(ex);
        }
    }

    [HttpPut("notifications/read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarcarTodasLeidas(CancellationToken ct)
    {
        try
        {
            await admin.MarcarTodasNotificacionesLeidasAsync(ObtenerUsuarioId(), ct);
            return NoContent();
        }
        catch (ExcepcionAdminOps ex)
        {
            return ProblemFrom(ex);
        }
        catch (ExcepcionAutenticacion ex)
        {
            return ProblemAuth(ex);
        }
    }

    [HttpGet("eco/redemptions")]
    [ProducesResponseType(typeof(IReadOnlyList<CanjeAdminDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarCanjes(
        [FromQuery(Name = "limit")] int limite = 50,
        CancellationToken ct = default)
    {
        try
        {
            return Ok(await ecoTokens.ListarCanjesAdminAsync(limite, ct));
        }
        catch (ExcepcionEcoTokens ex)
        {
            return Problem(
                detail: ex.Message,
                statusCode: ex.CodigoEstado,
                title: ex.Titulo,
                type: $"https://httpstatuses.com/{ex.CodigoEstado}",
                extensions: new Dictionary<string, object?> { ["code"] = ex.Codigo });
        }
    }

    [HttpGet("settings")]
    [ProducesResponseType(typeof(ConfiguracionUniversidadDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObtenerSettings(CancellationToken ct)
    {
        try
        {
            return Ok(await admin.ObtenerConfiguracionAsync(ct));
        }
        catch (ExcepcionAdminOps ex)
        {
            return ProblemFrom(ex);
        }
    }

    [HttpPut("settings")]
    [ProducesResponseType(typeof(ConfiguracionUniversidadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ActualizarSettings(
        [FromBody] SolicitudActualizarConfiguracion solicitud,
        CancellationToken ct)
    {
        try
        {
            return Ok(await admin.ActualizarConfiguracionAsync(solicitud, ct));
        }
        catch (ExcepcionAdminOps ex)
        {
            return ProblemFrom(ex);
        }
    }

    private Guid ObtenerUsuarioId()
    {
        var sub = User.FindFirstValue("sub")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw ExcepcionAutenticacion.NoAutorizado("Missing subject claim.");

        if (!Guid.TryParse(sub, out var usuarioId))
        {
            throw ExcepcionAutenticacion.NoAutorizado("Invalid subject claim.");
        }

        return usuarioId;
    }

    private ObjectResult ProblemFrom(ExcepcionAdminOps ex) =>
        Problem(
            detail: ex.Message,
            statusCode: ex.CodigoEstado,
            title: ex.Titulo,
            type: $"https://httpstatuses.com/{ex.CodigoEstado}",
            extensions: new Dictionary<string, object?> { ["code"] = ex.Codigo });

    private ObjectResult ProblemAuth(ExcepcionAutenticacion ex) =>
        Problem(
            detail: ex.Message,
            statusCode: ex.CodigoEstado,
            title: ex.Titulo,
            type: $"https://httpstatuses.com/{ex.CodigoEstado}",
            extensions: new Dictionary<string, object?> { ["code"] = ex.Codigo });
}
