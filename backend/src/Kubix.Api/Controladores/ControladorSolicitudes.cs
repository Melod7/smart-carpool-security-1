using System.Security.Claims;
using Kubix.Application.Auth;
using Kubix.Application.Tenancy;
using Kubix.Application.Viajes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kubix.Api.Controladores;

[ApiController]
[Authorize]
[Route("requests")]
public sealed class ControladorSolicitudes(IServicioSolicitudesViaje solicitudes) : ControllerBase
{
    [HttpPost("{id:guid}/accept")]
    [Authorize(Policy = NombresPoliticas.SoloConductor)]
    [ProducesResponseType(typeof(SolicitudViajeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Aceptar(Guid id, CancellationToken ct)
    {
        try
        {
            var resultado = await solicitudes.AceptarAsync(ObtenerUsuarioId(), id, ct);
            return Ok(resultado);
        }
        catch (ExcepcionViajes ex)
        {
            return ProblemViajes(ex);
        }
        catch (ExcepcionAutenticacion ex)
        {
            return ProblemAuth(ex);
        }
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = NombresPoliticas.SoloConductor)]
    [ProducesResponseType(typeof(SolicitudViajeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Rechazar(Guid id, CancellationToken ct)
    {
        try
        {
            var resultado = await solicitudes.RechazarAsync(ObtenerUsuarioId(), id, ct);
            return Ok(resultado);
        }
        catch (ExcepcionViajes ex)
        {
            return ProblemViajes(ex);
        }
        catch (ExcepcionAutenticacion ex)
        {
            return ProblemAuth(ex);
        }
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = NombresPoliticas.SoloPasajero)]
    [ProducesResponseType(typeof(SolicitudViajeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancelar(Guid id, CancellationToken ct)
    {
        try
        {
            var resultado = await solicitudes.CancelarAsync(ObtenerUsuarioId(), id, ct);
            return Ok(resultado);
        }
        catch (ExcepcionViajes ex)
        {
            return ProblemViajes(ex);
        }
        catch (ExcepcionAutenticacion ex)
        {
            return ProblemAuth(ex);
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

    private ObjectResult ProblemAuth(ExcepcionAutenticacion ex) =>
        Problem(
            detail: ex.Message,
            statusCode: ex.CodigoEstado,
            title: ex.Titulo,
            type: $"https://httpstatuses.com/{ex.CodigoEstado}",
            extensions: new Dictionary<string, object?> { ["code"] = ex.Codigo });
}
