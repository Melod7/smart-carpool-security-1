using System.Security.Claims;
using Kubix.Application.Auth;
using Kubix.Application.Tenancy;
using Kubix.Application.Tracking;
using Kubix.Application.Viajes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kubix.Api.Controladores;

[ApiController]
[Authorize]
[Route("trips")]
public sealed class ControladorViajes(
    IServicioViajes viajes,
    IServicioSolicitudesViaje solicitudes,
    IServicioTracking tracking) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = NombresPoliticas.SoloConductor)]
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
            return ProblemAuth(ex);
        }
    }

    [HttpGet("available")]
    [Authorize(Policy = NombresPoliticas.SoloPasajero)]
    [ProducesResponseType(typeof(IReadOnlyList<ViajeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListarDisponibles(CancellationToken ct)
    {
        try
        {
            var lista = await solicitudes.ListarDisponiblesAsync(ObtenerUsuarioId(), ct);
            return Ok(lista);
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

    [HttpGet("mine")]
    [Authorize(Policy = NombresPoliticas.UsuarioMobile)]
    [ProducesResponseType(typeof(MisViajesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ListarMios(
        [FromQuery] string? period,
        CancellationToken ct)
    {
        try
        {
            var resultado = await viajes.ListarMisViajesAsync(ObtenerUsuarioId(), period, ct);
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

    [HttpPost("{id:guid}/start")]
    [Authorize(Policy = NombresPoliticas.SoloConductor)]
    [ProducesResponseType(typeof(ViajeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Iniciar(Guid id, CancellationToken ct)
    {
        try
        {
            var viaje = await viajes.IniciarViajeAsync(ObtenerUsuarioId(), id, ct);
            return Ok(viaje);
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

    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = NombresPoliticas.SoloConductor)]
    [ProducesResponseType(typeof(ViajeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Completar(Guid id, CancellationToken ct)
    {
        try
        {
            var viaje = await viajes.CompletarViajeAsync(ObtenerUsuarioId(), id, ct);
            return Ok(viaje);
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
    [Authorize(Policy = NombresPoliticas.SoloConductor)]
    [ProducesResponseType(typeof(ViajeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancelar(Guid id, CancellationToken ct)
    {
        try
        {
            var viaje = await viajes.CancelarViajeAsync(ObtenerUsuarioId(), id, ct);
            return Ok(viaje);
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

    [HttpPost("{id:guid}/requests")]
    [Authorize(Policy = NombresPoliticas.SoloPasajero)]
    [ProducesResponseType(typeof(SolicitudViajeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CrearSolicitud(
        Guid id,
        [FromBody] SolicitudCrearSolicitudViaje solicitud,
        CancellationToken ct)
    {
        try
        {
            var creada = await solicitudes.CrearSolicitudAsync(ObtenerUsuarioId(), id, solicitud, ct);
            return Created($"/requests/{creada.Id}", creada);
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

    [HttpGet("{id:guid}/requests")]
    [Authorize(Policy = NombresPoliticas.SoloConductor)]
    [ProducesResponseType(typeof(IReadOnlyList<SolicitudViajeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListarSolicitudes(Guid id, CancellationToken ct)
    {
        try
        {
            var lista = await solicitudes.ListarPorViajeAsync(ObtenerUsuarioId(), id, ct);
            return Ok(lista);
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

    [HttpPost("{id:guid}/pings")]
    [Authorize(Policy = NombresPoliticas.UsuarioMobile)]
    [ProducesResponseType(typeof(PingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RegistrarPing(
        Guid id,
        [FromBody] SolicitudPing solicitud,
        CancellationToken ct)
    {
        try
        {
            var ping = await tracking.RegistrarPingAsync(ObtenerUsuarioId(), id, solicitud, ct);
            return Created($"/trips/{id}/pings/{ping.Id}", ping);
        }
        catch (ExcepcionTracking ex)
        {
            return ProblemTracking(ex);
        }
        catch (ExcepcionAutenticacion ex)
        {
            return ProblemAuth(ex);
        }
    }

    [HttpGet("{id:guid}/tracking")]
    [Authorize(Policy = NombresPoliticas.UsuarioMobile)]
    [ProducesResponseType(typeof(TrackingViajeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ObtenerTracking(Guid id, CancellationToken ct)
    {
        try
        {
            var resultado = await tracking.ObtenerTrackingAsync(ObtenerUsuarioId(), id, ct);
            return Ok(resultado);
        }
        catch (ExcepcionTracking ex)
        {
            return ProblemTracking(ex);
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

    private ObjectResult ProblemTracking(ExcepcionTracking ex) =>
        Problem(
            detail: ex.Message,
            statusCode: ex.CodigoEstado,
            title: ex.Titulo,
            type: $"https://httpstatuses.com/{ex.CodigoEstado}",
            extensions: new Dictionary<string, object?> { ["code"] = ex.Codigo });
}
