using System.Security.Claims;
using Kubix.Application.Auth;
using Kubix.Application.Sos;
using Kubix.Application.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kubix.Api.Controladores;

[ApiController]
[Authorize(Policy = NombresPoliticas.SoloCoordinador)]
[Route("admin/sos")]
public sealed class ControladorAdminSos(IServicioSos sos) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AlertaSosAdminDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Listar(CancellationToken ct)
    {
        try
        {
            var lista = await sos.ListarAdminAsync(ct);
            return Ok(lista);
        }
        catch (ExcepcionSos ex)
        {
            return ProblemSos(ex);
        }
    }

    [HttpPost("{id:guid}/resolve")]
    [ProducesResponseType(typeof(AlertaSosDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Resolver(Guid id, CancellationToken ct)
    {
        try
        {
            var resuelta = await sos.ResolverAdminAsync(ObtenerUsuarioId(), id, ct);
            return Ok(resuelta);
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
