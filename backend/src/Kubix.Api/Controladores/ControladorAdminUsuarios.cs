using System.Text;
using Kubix.Application.Tenancy;
using Kubix.Application.Usuarios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kubix.Api.Controladores;

[ApiController]
[Authorize(Policy = NombresPoliticas.SoloCoordinador)]
[Route("admin")]
public sealed class ControladorAdminUsuarios(IServicioRegistroUsuarios registro) : ControllerBase
{
    [HttpGet("registration-requests")]
    [ProducesResponseType(typeof(IReadOnlyList<SolicitudRegistroResumenDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarSolicitudes(CancellationToken ct)
    {
        try
        {
            var lista = await registro.ListarSolicitudesPendientesAsync(ct);
            return Ok(lista);
        }
        catch (ExcepcionRegistroUsuarios ex)
        {
            return ProblemFrom(ex);
        }
    }

    [HttpPost("registration-requests/{id:guid}/accept")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AceptarSolicitud(Guid id, CancellationToken ct)
    {
        try
        {
            await registro.AceptarSolicitudAsync(id, ct);
            return NoContent();
        }
        catch (ExcepcionRegistroUsuarios ex)
        {
            return ProblemFrom(ex);
        }
    }

    [HttpPost("registration-requests/{id:guid}/deny")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DenegarSolicitud(Guid id, CancellationToken ct)
    {
        try
        {
            await registro.DenegarSolicitudAsync(id, ct);
            return NoContent();
        }
        catch (ExcepcionRegistroUsuarios ex)
        {
            return ProblemFrom(ex);
        }
    }

    [HttpGet("users")]
    [ProducesResponseType(typeof(IReadOnlyList<UsuarioAdminDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarUsuarios(
        [FromQuery(Name = "status")] string? estado,
        [FromQuery(Name = "role")] string? rol,
        [FromQuery(Name = "campus")] Guid? campusId,
        [FromQuery(Name = "search")] string? busqueda,
        [FromQuery(Name = "page")] int pagina = 1,
        [FromQuery(Name = "pageSize")] int tamanoPagina = 20,
        CancellationToken ct = default)
    {
        try
        {
            var paginaResultado = await registro.ListarUsuariosAsync(
                new FiltroUsuariosAdmin
                {
                    Estado = estado,
                    Rol = rol,
                    CampusId = campusId,
                    Busqueda = busqueda,
                    Pagina = pagina,
                    TamanoPagina = tamanoPagina
                },
                ct);

            Response.Headers["X-Total-Count"] = paginaResultado.Total.ToString();
            return Ok(paginaResultado.Items);
        }
        catch (ExcepcionRegistroUsuarios ex)
        {
            return ProblemFrom(ex);
        }
    }

    [HttpGet("users/export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportarUsuarios(
        [FromQuery(Name = "status")] string? estado,
        [FromQuery(Name = "role")] string? rol,
        [FromQuery(Name = "campus")] Guid? campusId,
        [FromQuery(Name = "search")] string? busqueda,
        CancellationToken ct = default)
    {
        try
        {
            var csv = await registro.ExportarUsuariosCsvAsync(
                new FiltroUsuariosAdmin
                {
                    Estado = estado,
                    Rol = rol,
                    CampusId = campusId,
                    Busqueda = busqueda,
                    Pagina = 1,
                    TamanoPagina = 100
                },
                ct);

            var bytes = Encoding.UTF8.GetBytes(csv);
            return File(bytes, "text/csv", "kubix_users.csv");
        }
        catch (ExcepcionRegistroUsuarios ex)
        {
            return ProblemFrom(ex);
        }
    }

    [HttpPost("users/{id:guid}/block")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> BloquearUsuario(Guid id, CancellationToken ct)
    {
        try
        {
            await registro.BloquearUsuarioAsync(id, ct);
            return NoContent();
        }
        catch (ExcepcionRegistroUsuarios ex)
        {
            return ProblemFrom(ex);
        }
    }

    [HttpPost("users/{id:guid}/unblock")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DesbloquearUsuario(Guid id, CancellationToken ct)
    {
        try
        {
            await registro.DesbloquearUsuarioAsync(id, ct);
            return NoContent();
        }
        catch (ExcepcionRegistroUsuarios ex)
        {
            return ProblemFrom(ex);
        }
    }

    private ObjectResult ProblemFrom(ExcepcionRegistroUsuarios ex) =>
        Problem(
            detail: ex.Message,
            statusCode: ex.CodigoEstado,
            title: ex.Titulo,
            type: $"https://httpstatuses.com/{ex.CodigoEstado}",
            extensions: new Dictionary<string, object?> { ["code"] = ex.Codigo });
}
