using System.Security.Claims;
using Kubix.Application.Auth;
using Kubix.Application.Sos;
using Kubix.Application.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kubix.Api.Controladores;

[ApiController]
[Authorize]
[Route("sos")]
public sealed class ControladorSos(IServicioSos sos) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = NombresPoliticas.UsuarioMobile)]
    [ProducesResponseType(typeof(AlertaSosDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Crear(
        [FromBody] SolicitudCrearSos solicitud,
        CancellationToken ct)
    {
        try
        {
            var creada = await sos.CrearAsync(ObtenerUsuarioId(), solicitud, ct);
            return Created($"/sos/{creada.Id}", creada);
        }
        catch (ExcepcionSos ex)
        {
            return ProblemSos(ex);
        }
        catch (ExcepcionAutenticacion ex)
        {
            return ProblemAuth(ex);
        }
    }

    [HttpPost("{id:guid}/close")]
    [Authorize(Policy = NombresPoliticas.UsuarioMobile)]
    [ProducesResponseType(typeof(AlertaSosDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cerrar(Guid id, CancellationToken ct)
    {
        try
        {
            var cerrada = await sos.CerrarAsync(ObtenerUsuarioId(), id, ct);
            return Ok(cerrada);
        }
        catch (ExcepcionSos ex)
        {
            return ProblemSos(ex);
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

    private ObjectResult ProblemSos(ExcepcionSos ex) =>
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
