using System.Security.Claims;
using Kubix.Application.Auth;
using Kubix.Application.Calificaciones;
using Kubix.Application.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kubix.Api.Controladores;

[ApiController]
[Authorize]
public sealed class ControladorCalificaciones(IServicioCalificaciones calificaciones) : ControllerBase
{
    [HttpPost("trips/{id:guid}/ratings")]
    [Authorize(Policy = NombresPoliticas.UsuarioMobile)]
    [ProducesResponseType(typeof(CalificacionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Crear(
        Guid id,
        [FromBody] SolicitudCrearCalificacion solicitud,
        CancellationToken ct)
    {
        try
        {
            var creada = await calificaciones.CrearAsync(ObtenerUsuarioId(), id, solicitud, ct);
            return Created($"/trips/{id}/ratings/{creada.Id}", creada);
        }
        catch (ExcepcionCalificaciones ex)
        {
            return ProblemCalificaciones(ex);
        }
        catch (ExcepcionAutenticacion ex)
        {
            return ProblemAuth(ex);
        }
    }

    [HttpGet("ratings/pending")]
    [Authorize(Policy = NombresPoliticas.UsuarioMobile)]
    [ProducesResponseType(typeof(IReadOnlyList<CalificacionPendienteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListarPendientes(CancellationToken ct)
    {
        try
        {
            var lista = await calificaciones.ListarPendientesAsync(ObtenerUsuarioId(), ct);
            return Ok(lista);
        }
        catch (ExcepcionCalificaciones ex)
        {
            return ProblemCalificaciones(ex);
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

    private ObjectResult ProblemCalificaciones(ExcepcionCalificaciones ex) =>
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
