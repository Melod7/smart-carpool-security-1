using Kubix.Application.Usuarios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kubix.Api.Controladores;

[ApiController]
[Route("public")]
[AllowAnonymous]
public sealed class ControladorPublico(IServicioRegistroUsuarios registro) : ControllerBase
{
    [HttpGet("universities")]
    [ProducesResponseType(typeof(IReadOnlyList<UniversidadPublicaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarUniversidades(CancellationToken ct)
    {
        var lista = await registro.ListarUniversidadesPublicasAsync(ct);
        return Ok(lista);
    }
}
