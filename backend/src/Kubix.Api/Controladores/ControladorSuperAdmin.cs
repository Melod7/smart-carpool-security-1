using Kubix.Application.SuperAdmin;
using Kubix.Application.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kubix.Api.Controladores;

[ApiController]
[Authorize(Policy = NombresPoliticas.SoloSuperAdmin)]
[Route("super")]
public sealed class ControladorSuperAdmin(IServicioSuperAdmin superAdmin) : ControllerBase
{
    [HttpGet("universities")]
    [ProducesResponseType(typeof(IReadOnlyList<UniversidadResumenDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarUniversidades(CancellationToken ct)
    {
        var lista = await superAdmin.ListarUniversidadesAsync(ct);
        return Ok(lista);
    }

    [HttpPost("universities")]
    [ProducesResponseType(typeof(UniversidadDetalleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CrearUniversidad(
        [FromBody] SolicitudCrearUniversidad solicitud,
        CancellationToken ct)
    {
        try
        {
            var creada = await superAdmin.CrearUniversidadAsync(solicitud, ct);
            return Created($"/super/universities/{creada.Id}", creada);
        }
        catch (ExcepcionSuperAdmin ex)
        {
            return ProblemFrom(ex);
        }
    }

    [HttpPut("universities/{id:guid}")]
    [ProducesResponseType(typeof(UniversidadDetalleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ActualizarUniversidad(
        Guid id,
        [FromBody] SolicitudActualizarUniversidad solicitud,
        CancellationToken ct)
    {
        try
        {
            var actualizada = await superAdmin.ActualizarUniversidadAsync(id, solicitud, ct);
            return Ok(actualizada);
        }
        catch (ExcepcionSuperAdmin ex)
        {
            return ProblemFrom(ex);
        }
    }

    [HttpPost("universities/{id:guid}/suspend")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SuspenderUniversidad(Guid id, CancellationToken ct)
    {
        try
        {
            await superAdmin.SuspenderUniversidadAsync(id, ct);
            return NoContent();
        }
        catch (ExcepcionSuperAdmin ex)
        {
            return ProblemFrom(ex);
        }
    }

    [HttpGet("universities/{id:guid}/campuses")]
    [ProducesResponseType(typeof(IReadOnlyList<CampusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListarCampuses(Guid id, CancellationToken ct)
    {
        try
        {
            var lista = await superAdmin.ListarCampusesAsync(id, ct);
            return Ok(lista);
        }
        catch (ExcepcionSuperAdmin ex)
        {
            return ProblemFrom(ex);
        }
    }

    [HttpPost("universities/{id:guid}/campuses")]
    [ProducesResponseType(typeof(CampusDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CrearCampus(
        Guid id,
        [FromBody] SolicitudCrearCampus solicitud,
        CancellationToken ct)
    {
        try
        {
            var campus = await superAdmin.CrearCampusAsync(id, solicitud, ct);
            return Created($"/super/universities/{id}/campuses/{campus.Id}", campus);
        }
        catch (ExcepcionSuperAdmin ex)
        {
            return ProblemFrom(ex);
        }
    }

    [HttpPut("universities/{universityId:guid}/campuses/{campusId:guid}")]
    [ProducesResponseType(typeof(CampusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ActualizarCampus(
        Guid universityId,
        Guid campusId,
        [FromBody] SolicitudActualizarCampus solicitud,
        CancellationToken ct)
    {
        try
        {
            var campus = await superAdmin.ActualizarCampusAsync(universityId, campusId, solicitud, ct);
            return Ok(campus);
        }
        catch (ExcepcionSuperAdmin ex)
        {
            return ProblemFrom(ex);
        }
    }

    [HttpDelete("universities/{universityId:guid}/campuses/{campusId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EliminarCampus(Guid universityId, Guid campusId, CancellationToken ct)
    {
        try
        {
            await superAdmin.EliminarCampusAsync(universityId, campusId, ct);
            return NoContent();
        }
        catch (ExcepcionSuperAdmin ex)
        {
            return ProblemFrom(ex);
        }
    }

    [HttpGet("universities/{id:guid}/coordinadores")]
    [ProducesResponseType(typeof(IReadOnlyList<CoordinadorDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListarCoordinadores(Guid id, CancellationToken ct)
    {
        try
        {
            var lista = await superAdmin.ListarCoordinadoresAsync(id, ct);
            return Ok(lista);
        }
        catch (ExcepcionSuperAdmin ex)
        {
            return ProblemFrom(ex);
        }
    }

    [HttpPost("universities/{id:guid}/coordinadores")]
    [ProducesResponseType(typeof(CoordinadorCreadoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CrearCoordinador(
        Guid id,
        [FromBody] SolicitudCrearCoordinador solicitud,
        CancellationToken ct)
    {
        try
        {
            var creado = await superAdmin.CrearCoordinadorAsync(id, solicitud, ct);
            return Created($"/super/coordinadores/{creado.Id}", creado);
        }
        catch (ExcepcionSuperAdmin ex)
        {
            return ProblemFrom(ex);
        }
    }

    [HttpPost("coordinadores/{id:guid}/reset-password")]
    [ProducesResponseType(typeof(RespuestaResetContrasena), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetearContrasena(Guid id, CancellationToken ct)
    {
        try
        {
            var respuesta = await superAdmin.ResetearContrasenaCoordinadorAsync(id, ct);
            return Ok(respuesta);
        }
        catch (ExcepcionSuperAdmin ex)
        {
            return ProblemFrom(ex);
        }
    }

    [HttpGet("stats")]
    [ProducesResponseType(typeof(StatsSuperAdminDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObtenerStats(CancellationToken ct)
    {
        var stats = await superAdmin.ObtenerStatsAsync(ct);
        return Ok(stats);
    }

    private ObjectResult ProblemFrom(ExcepcionSuperAdmin ex) =>
        Problem(
            detail: ex.Message,
            statusCode: ex.CodigoEstado,
            title: ex.Titulo,
            type: $"https://httpstatuses.com/{ex.CodigoEstado}",
            extensions: new Dictionary<string, object?> { ["code"] = ex.Codigo });
}
