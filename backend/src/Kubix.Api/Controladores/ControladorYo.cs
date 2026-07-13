using System.Security.Claims;
using Kubix.Application.Auth;
using Kubix.Application.Usuarios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kubix.Api.Controladores;

[ApiController]
[Authorize]
[Route("me")]
public sealed class ControladorYo(
    IServicioAutenticacion autenticacion,
    IServicioRegistroUsuarios registro) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ResumenUsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ObtenerYo(CancellationToken ct)
    {
        try
        {
            var perfil = await autenticacion.ObtenerPerfilAsync(ObtenerUsuarioId(), ct);
            return Ok(perfil);
        }
        catch (ExcepcionAutenticacion ex)
        {
            return ProblemAuth(ex);
        }
    }

    [HttpGet("profile")]
    [ProducesResponseType(typeof(PerfilUsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerPerfil(CancellationToken ct)
    {
        try
        {
            var perfil = await registro.ObtenerPerfilAsync(ObtenerUsuarioId(), ct);
            return Ok(perfil);
        }
        catch (ExcepcionRegistroUsuarios ex)
        {
            return ProblemRegistro(ex);
        }
        catch (ExcepcionAutenticacion ex)
        {
            return ProblemAuth(ex);
        }
    }

    [HttpPut("profile")]
    [ProducesResponseType(typeof(PerfilUsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ActualizarPerfil(
        [FromBody] SolicitudActualizarPerfil solicitud,
        CancellationToken ct)
    {
        try
        {
            var perfil = await registro.ActualizarPerfilAsync(ObtenerUsuarioId(), solicitud, ct);
            return Ok(perfil);
        }
        catch (ExcepcionRegistroUsuarios ex)
        {
            return ProblemRegistro(ex);
        }
        catch (ExcepcionAutenticacion ex)
        {
            return ProblemAuth(ex);
        }
    }

    [HttpGet("emergency-contacts")]
    [ProducesResponseType(typeof(IReadOnlyList<ContactoEmergenciaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListarContactos(CancellationToken ct)
    {
        try
        {
            var lista = await registro.ListarContactosEmergenciaAsync(ObtenerUsuarioId(), ct);
            return Ok(lista);
        }
        catch (ExcepcionRegistroUsuarios ex)
        {
            return ProblemRegistro(ex);
        }
        catch (ExcepcionAutenticacion ex)
        {
            return ProblemAuth(ex);
        }
    }

    [HttpPost("emergency-contacts")]
    [ProducesResponseType(typeof(ContactoEmergenciaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CrearContacto(
        [FromBody] SolicitudCrearContactoEmergencia solicitud,
        CancellationToken ct)
    {
        try
        {
            var creado = await registro.CrearContactoEmergenciaAsync(ObtenerUsuarioId(), solicitud, ct);
            return Created($"/me/emergency-contacts/{creado.Id}", creado);
        }
        catch (ExcepcionRegistroUsuarios ex)
        {
            return ProblemRegistro(ex);
        }
        catch (ExcepcionAutenticacion ex)
        {
            return ProblemAuth(ex);
        }
    }

    [HttpDelete("emergency-contacts/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EliminarContacto(Guid id, CancellationToken ct)
    {
        try
        {
            await registro.EliminarContactoEmergenciaAsync(ObtenerUsuarioId(), id, ct);
            return NoContent();
        }
        catch (ExcepcionRegistroUsuarios ex)
        {
            return ProblemRegistro(ex);
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

    private ObjectResult ProblemAuth(ExcepcionAutenticacion ex) =>
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
