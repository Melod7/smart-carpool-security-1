using Kubix.Application.Tenancy;
using Kubix.Application.Tracking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kubix.Api.Controladores;

[ApiController]
[Authorize(Policy = NombresPoliticas.SoloCoordinador)]
[Route("admin/tracking")]
public sealed class ControladorAdminTracking(IServicioTracking tracking) : ControllerBase
{
    [HttpGet("active")]
    [ProducesResponseType(typeof(TrackingAdminActivoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListarActivos(CancellationToken ct)
    {
        try
        {
            var activos = await tracking.ListarActivosAdminAsync(ct);
            return Ok(activos);
        }
        catch (ExcepcionTracking ex)
        {
            return ProblemTracking(ex);
        }
    }

    private ObjectResult ProblemTracking(ExcepcionTracking ex) =>
        Problem(
            detail: ex.Message,
            statusCode: ex.CodigoEstado,
            title: ex.Titulo,
            type: $"https://httpstatuses.com/{ex.CodigoEstado}",
            extensions: new Dictionary<string, object?> { ["code"] = ex.Codigo });
}
