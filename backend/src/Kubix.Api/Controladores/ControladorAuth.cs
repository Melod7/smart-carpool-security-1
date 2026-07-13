using System.Security.Claims;
using Kubix.Application.Auth;
using Kubix.Application.Usuarios;
using Microsoft.AspNetCore.Mvc;

namespace Kubix.Api.Controladores;

[ApiController]
[Route("auth")]
public sealed class ControladorAuth(
    IServicioAutenticacion autenticacion,
    IServicioRegistroUsuarios registro) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType(typeof(RespuestaRegistroDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Register([FromBody] SolicitudRegistroDto solicitud, CancellationToken ct)
    {
        try
        {
            var respuesta = await registro.RegistrarAsync(solicitud, ct);
            return Accepted(respuesta);
        }
        catch (ExcepcionRegistroUsuarios ex)
        {
            return ProblemRegistro(ex);
        }
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(RespuestaAutenticacion), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Login([FromBody] SolicitudInicioSesion solicitud, CancellationToken ct)
    {
        try
        {
            var respuesta = await autenticacion.IniciarSesionAsync(solicitud, ct);
            return Ok(respuesta);
        }
        catch (ExcepcionAutenticacion ex)
        {
            return ProblemFrom(ex);
        }
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(RespuestaAutenticacion), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Refresh([FromBody] SolicitudRefresco solicitud, CancellationToken ct)
    {
        try
        {
            var respuesta = await autenticacion.RefrescarAsync(solicitud, ct);
            return Ok(respuesta);
        }
        catch (ExcepcionAutenticacion ex)
        {
            return ProblemFrom(ex);
        }
    }

    [HttpPost("logout")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout([FromBody] SolicitudCierreSesion? solicitud, CancellationToken ct)
    {
        try
        {
            var usuarioId = ObtenerUsuarioId();
            await autenticacion.CerrarSesionAsync(usuarioId, solicitud ?? new SolicitudCierreSesion(), ct);
            return NoContent();
        }
        catch (ExcepcionAutenticacion ex)
        {
            return ProblemFrom(ex);
        }
    }

    [HttpPost("change-password")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] SolicitudCambioContrasena solicitud,
        CancellationToken ct)
    {
        try
        {
            var usuarioId = ObtenerUsuarioId();
            await autenticacion.CambiarContrasenaAsync(usuarioId, solicitud, ct);
            return NoContent();
        }
        catch (ExcepcionAutenticacion ex)
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

    private ObjectResult ProblemFrom(ExcepcionAutenticacion ex) =>
        Problem(
            detail: ex.Message,
            statusCode: ex.CodigoEstado,
            title: ex.Titulo,
            type: $"https://httpstatuses.com/{ex.CodigoEstado}",
            extensions: new Dictionary<string, object?> { ["code"] = ex.Codigo });

    private ObjectResult ProblemRegistro(ExcepcionRegistroUsuarios ex) =>
        Problem(
            detail: ex.Message,
            statusCode: ex.CodigoEstado,
            title: ex.Titulo,
            type: $"https://httpstatuses.com/{ex.CodigoEstado}",
            extensions: new Dictionary<string, object?> { ["code"] = ex.Codigo });
}
