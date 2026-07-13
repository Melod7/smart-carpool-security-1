using System.Security.Claims;
using Kubix.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kubix.Api.Controladores;

[ApiController]
[Authorize]
[Route("me")]
public sealed class ControladorYo(IServicioAutenticacion autenticacion) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ResumenUsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ObtenerPerfil(CancellationToken ct)
    {
        try
        {
            var sub = User.FindFirstValue("sub")
                ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw ExcepcionAutenticacion.NoAutorizado("Missing subject claim.");

            if (!Guid.TryParse(sub, out var usuarioId))
            {
                throw ExcepcionAutenticacion.NoAutorizado("Invalid subject claim.");
            }

            var perfil = await autenticacion.ObtenerPerfilAsync(usuarioId, ct);
            return Ok(perfil);
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
}
