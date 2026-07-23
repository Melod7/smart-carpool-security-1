using System.Security.Claims;
using Kubix.Application.Auth;
using Kubix.Application.EcoTokens;
using Kubix.Application.Tenancy;
using Kubix.Application.Usuarios;
using Kubix.Application.Viajes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kubix.Api.Controladores;

[ApiController]
[Authorize]
[Route("me")]
public sealed class ControladorYo(
    IServicioAutenticacion autenticacion,
    IServicioRegistroUsuarios registro,
    IServicioViajes viajes,
    IServicioEcoTokens ecoTokens) : ControllerBase
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

    [HttpPost("profile-change")]
    [Authorize(Policy = NombresPoliticas.SoloPasajero)]
    [ProducesResponseType(typeof(RespuestaCambioPerfilDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SolicitarCambioPerfil(
        [FromBody] SolicitudActualizarPerfil solicitud,
        CancellationToken ct)
    {
        try
        {
            var respuesta = await registro.SolicitarCambioPerfilAsync(
                ObtenerUsuarioId(),
                solicitud,
                ct);
            return Accepted(respuesta);
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

    [HttpPost("mode")]
    [Authorize(Policy = NombresPoliticas.UsuarioMobile)]
    [ProducesResponseType(typeof(RespuestaCambioModoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RespuestaCambioModoDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CambiarModo(
        [FromBody] SolicitudCambioModo solicitud,
        CancellationToken ct)
    {
        try
        {
            var respuesta = await registro.CambiarModoAsync(ObtenerUsuarioId(), solicitud, ct);
            return respuesta.Estado == "pending" ? Accepted(respuesta) : Ok(respuesta);
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

    [HttpGet("eco")]
    [Authorize(Policy = NombresPoliticas.UsuarioMobile)]
    [ProducesResponseType(typeof(ResumenEcoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerEco(
        [FromQuery(Name = "page")] int pagina = 1,
        [FromQuery(Name = "pageSize")] int tamanoPagina = 20,
        CancellationToken ct = default)
    {
        try
        {
            var resumen = await ecoTokens.ObtenerResumenAsync(ObtenerUsuarioId(), pagina, tamanoPagina, ct);
            return Ok(resumen);
        }
        catch (ExcepcionEcoTokens ex)
        {
            return ProblemEco(ex);
        }
        catch (ExcepcionAutenticacion ex)
        {
            return ProblemAuth(ex);
        }
    }

    [HttpPost("eco/redeem")]
    [Authorize(Policy = NombresPoliticas.UsuarioMobile)]
    [ProducesResponseType(typeof(ResultadoCanjePremioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CanjearPremio(
        [FromBody] SolicitudCanjePremio solicitud,
        CancellationToken ct)
    {
        try
        {
            var resultado = await ecoTokens.CanjearPremioAsync(
                ObtenerUsuarioId(),
                solicitud.CodigoPremio,
                ct);
            return Ok(resultado);
        }
        catch (ExcepcionEcoTokens ex)
        {
            return ProblemEco(ex);
        }
        catch (ExcepcionAutenticacion ex)
        {
            return ProblemAuth(ex);
        }
    }

    [HttpGet("vehicle")]
    [Authorize(Policy = NombresPoliticas.SoloConductor)]
    [ProducesResponseType(typeof(VehiculoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerVehiculo(CancellationToken ct)
    {
        try
        {
            var vehiculo = await viajes.ObtenerVehiculoAsync(ObtenerUsuarioId(), ct);
            return Ok(vehiculo);
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

    [HttpPut("vehicle")]
    [Authorize(Policy = NombresPoliticas.SoloConductor)]
    [ProducesResponseType(typeof(VehiculoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(CambioVehiculoPendienteDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpsertVehiculo(
        [FromBody] SolicitudUpsertVehiculo solicitud,
        CancellationToken ct)
    {
        try
        {
            var resultado = await viajes.UpsertVehiculoAsync(ObtenerUsuarioId(), solicitud, ct);
            if (resultado.CambioPendiente is not null)
            {
                return Accepted(resultado.CambioPendiente);
            }

            return Ok(resultado.Vehiculo);
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

    private ObjectResult ProblemViajes(ExcepcionViajes ex) =>
        Problem(
            detail: ex.Message,
            statusCode: ex.CodigoEstado,
            title: ex.Titulo,
            type: $"https://httpstatuses.com/{ex.CodigoEstado}",
            extensions: new Dictionary<string, object?> { ["code"] = ex.Codigo });

    private ObjectResult ProblemEco(ExcepcionEcoTokens ex) =>
        Problem(
            detail: ex.Message,
            statusCode: ex.CodigoEstado,
            title: ex.Titulo,
            type: $"https://httpstatuses.com/{ex.CodigoEstado}",
            extensions: new Dictionary<string, object?> { ["code"] = ex.Codigo });
}
