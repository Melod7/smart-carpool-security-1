using Kubix.Application.Auth;
using Kubix.Domain;
using Kubix.Domain.Enums;
using Kubix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Kubix.Infrastructure.Auth;

public sealed class CacheEstadoUsuario(
    ContextoApp db,
    IMemoryCache cache) : ICacheEstadoUsuario
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    public async Task<EstadoSesionUsuario> ObtenerAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var clave = Clave(usuarioId);
        if (cache.TryGetValue(clave, out EstadoSesionUsuario? cacheado) && cacheado is not null)
        {
            return cacheado;
        }

        var fila = await db.Usuarios
            .AsNoTracking()
            .Where(u => u.Id == usuarioId)
            .Select(u => new
            {
                u.Id,
                u.Estado,
                u.DebeCambiarContrasena,
                EstadoUniversidad = u.Universidad != null ? (EstadoUniversidad?)u.Universidad.Estado : null
            })
            .FirstOrDefaultAsync(ct);

        EstadoSesionUsuario estado;
        if (fila is null)
        {
            estado = new EstadoSesionUsuario
            {
                UsuarioId = usuarioId,
                EstadoUsuario = "missing",
                Permitido = false,
                MotivoRechazo = "User not found."
            };
        }
        else
        {
            var estadoUsuario = ConversorEnumDominio.ACadenaDb(fila.Estado);
            string? estadoUniversidad = fila.EstadoUniversidad is null
                ? null
                : ConversorEnumDominio.ACadenaDb(fila.EstadoUniversidad.Value);

            var permitido = fila.Estado == EstadoUsuario.Activo
                && fila.EstadoUniversidad != EstadoUniversidad.Suspendida;

            string? motivo = null;
            if (fila.Estado == EstadoUsuario.Bloqueado)
            {
                motivo = "User is blocked.";
            }
            else if (fila.Estado == EstadoUsuario.Pendiente)
            {
                motivo = "User is pending approval.";
            }
            else if (fila.EstadoUniversidad == EstadoUniversidad.Suspendida)
            {
                motivo = "University is suspended.";
            }

            estado = new EstadoSesionUsuario
            {
                UsuarioId = fila.Id,
                EstadoUsuario = estadoUsuario,
                EstadoUniversidad = estadoUniversidad,
                DebeCambiarContrasena = fila.DebeCambiarContrasena,
                Permitido = permitido,
                MotivoRechazo = motivo
            };
        }

        cache.Set(clave, estado, Ttl);
        return estado;
    }

    public void Invalidar(Guid usuarioId) => cache.Remove(Clave(usuarioId));

    public async Task InvalidarUniversidadAsync(Guid universidadId, CancellationToken ct = default)
    {
        var ids = await db.Usuarios
            .AsNoTracking()
            .Where(u => u.UniversidadId == universidadId)
            .Select(u => u.Id)
            .ToListAsync(ct);

        foreach (var id in ids)
        {
            Invalidar(id);
        }
    }

    private static string Clave(Guid usuarioId) => $"auth:estado:{usuarioId:D}";
}
