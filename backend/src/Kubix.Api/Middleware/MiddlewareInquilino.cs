using System.Security.Claims;
using Kubix.Application.Tenancy;
using Kubix.Domain;
using Kubix.Domain.Enums;

namespace Kubix.Api.Middleware;

/// <summary>
/// Puebla <see cref="IContextoInquilino"/> desde claims JWT tras la autenticación.
/// </summary>
public sealed class MiddlewareInquilino(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext contexto, IContextoInquilino inquilino)
    {
        if (contexto.User.Identity?.IsAuthenticated == true)
        {
            PoblarDesdeClaims(contexto.User, inquilino);
        }

        await next(contexto);
    }

    internal static void PoblarDesdeClaims(ClaimsPrincipal usuario, IContextoInquilino inquilino)
    {
        var sub = usuario.FindFirstValue("sub")
            ?? usuario.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(sub) && Guid.TryParse(sub, out var usuarioId))
        {
            inquilino.UsuarioId = usuarioId;
        }

        var rolClaim = usuario.FindFirstValue("role")
            ?? usuario.FindFirstValue(ClaimTypes.Role);
        if (!string.IsNullOrWhiteSpace(rolClaim))
        {
            try
            {
                inquilino.Rol = ConversorEnumDominio.DesdeCadenaDb<RolUsuario>(rolClaim);
            }
            catch (ArgumentOutOfRangeException)
            {
                inquilino.Rol = null;
            }
        }

        var uniClaim = usuario.FindFirstValue("university_id");
        if (!string.IsNullOrWhiteSpace(uniClaim) && Guid.TryParse(uniClaim, out var universidadId))
        {
            inquilino.UniversidadId = universidadId;
        }

        var campusClaim = usuario.FindFirstValue("campus_id");
        if (!string.IsNullOrWhiteSpace(campusClaim) && Guid.TryParse(campusClaim, out var campusId))
        {
            inquilino.CampusId = campusId;
        }

        if (inquilino.Rol == RolUsuario.SuperAdministrador)
        {
            inquilino.OmitirFiltros = true;
        }
        else
        {
            inquilino.OmitirFiltros = false;
        }
    }
}
