using System.Security.Claims;
using Kubix.Application.Auth;
using Kubix.Application.Tenancy;
using Kubix.Application.Viajes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kubix.Api.Controladores;

[ApiController]
[Authorize(Policy = NombresPoliticas.SoloConductor)]
[Route("trips")]
public sealed class ControladorViajes(IServicioViajes viajes) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(ViajeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Publicar(
        [FromBody] SolicitudPublicarViaje solicitud,
        CancellationToken ct)
    {
        try
        {
            var viaje = await viajes.PublicarViajeAsync(ObtenerUsuarioId(), solicitud, ct);
            return Created($"/trips/{viaje.Id}", viaje);
        }
        catch (ExcepcionViajes ex)
        {
            return ProblemViajes(ex);
        }
        catch (ExcepcionAutenticacion ex)
        {
            return Problem(
                detail: ex.Message,
                statusCode: ex.CodigoEstado,
                title: ex.Titulo,
                type: $"https://httpstatuses.com/{ex.CodigoEstado}",
                extensions: new Dictionary<string, object?> { ["code"] = ex.Codigo });
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

    private ObjectResult ProblemViajes(ExcepcionViajes ex) =>
        Problem(
            detail: ex.Message,
            statusCode: ex.CodigoEstado,
            title: ex.Titulo,
            type: $"https://httpstatuses.com/{ex.CodigoEstado}",
            extensions: new Dictionary<string, object?> { ["code"] = ex.Codigo });
}
