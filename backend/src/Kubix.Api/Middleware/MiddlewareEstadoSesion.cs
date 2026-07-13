using System.Security.Claims;
using System.Text.Json;
using Kubix.Application.Auth;

namespace Kubix.Api.Middleware;

/// <summary>
/// Chequea estado de usuario/universidad (caché 30s) y fuerza change-password
/// cuando <c>must_change_password</c> está activo.
/// </summary>
public sealed class MiddlewareEstadoSesion(RequestDelegate next)
{
    private static readonly HashSet<string> RutasPermitidasConCambioForzado = new(StringComparer.OrdinalIgnoreCase)
    {
        "/auth/change-password",
        "/auth/logout",
        "/me"
    };

    public async Task InvokeAsync(HttpContext contexto, ICacheEstadoUsuario cacheEstado)
    {
        if (contexto.User.Identity?.IsAuthenticated != true)
        {
            await next(contexto);
            return;
        }

        var sub = contexto.User.FindFirstValue("sub")
            ?? contexto.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(sub, out var usuarioId))
        {
            await EscribirProblemaAsync(
                contexto,
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "Missing or invalid subject claim.",
                "invalid_token");
            return;
        }

        var estado = await cacheEstado.ObtenerAsync(usuarioId, contexto.RequestAborted);
        if (!estado.Permitido)
        {
            await EscribirProblemaAsync(
                contexto,
                StatusCodes.Status403Forbidden,
                "Forbidden",
                estado.MotivoRechazo ?? "Session is no longer valid.",
                "session_revoked");
            return;
        }

        if (estado.DebeCambiarContrasena && !RutaPermitidaConCambioForzado(contexto))
        {
            await EscribirProblemaAsync(
                contexto,
                StatusCodes.Status403Forbidden,
                "Forbidden",
                "Password change required before accessing this resource.",
                "must_change_password");
            return;
        }

        await next(contexto);
    }

    private static bool RutaPermitidaConCambioForzado(HttpContext contexto)
    {
        var ruta = contexto.Request.Path.Value ?? string.Empty;
        if (ruta.EndsWith('/') && ruta.Length > 1)
        {
            ruta = ruta.TrimEnd('/');
        }

        if (!RutasPermitidasConCambioForzado.Contains(ruta))
        {
            return false;
        }

        // GET /me permitido; POST /me u otros métodos no (solo lectura)
        if (ruta.Equals("/me", StringComparison.OrdinalIgnoreCase))
        {
            return HttpMethods.IsGet(contexto.Request.Method);
        }

        return HttpMethods.IsPost(contexto.Request.Method);
    }

    private static async Task EscribirProblemaAsync(
        HttpContext contexto,
        int status,
        string title,
        string detail,
        string code)
    {
        contexto.Response.StatusCode = status;
        contexto.Response.ContentType = "application/problem+json";

        var problema = new
        {
            type = $"https://httpstatuses.com/{status}",
            title,
            status,
            detail,
            code
        };

        await contexto.Response.WriteAsync(JsonSerializer.Serialize(problema));
    }
}
