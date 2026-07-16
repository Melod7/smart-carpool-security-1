using System.Security.Cryptography;
using System.Text;
using Kubix.Application.Auth;
using Kubix.Domain;
using Kubix.Domain.Entities;
using Kubix.Domain.Enums;
using Kubix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kubix.Infrastructure.Auth;

public sealed class ServicioAutenticacion(
    ContextoApp db,
    IServicioTokenJwt tokensJwt,
    ICacheEstadoUsuario cacheEstado,
    IOptions<OpcionesJwt> opcionesJwt) : IServicioAutenticacion
{
    private readonly OpcionesJwt _jwt = opcionesJwt.Value;

    public async Task<RespuestaAutenticacion> IniciarSesionAsync(
        SolicitudInicioSesion solicitud,
        CancellationToken ct = default)
    {
        var correo = (solicitud.Correo ?? string.Empty).Trim().ToLowerInvariant();
        var contrasena = solicitud.Contrasena ?? string.Empty;

        if (string.IsNullOrWhiteSpace(correo) || string.IsNullOrWhiteSpace(contrasena))
        {
            throw ExcepcionAutenticacion.Validacion("Email and password are required.");
        }

        var usuario = await db.Usuarios
            .Include(u => u.Universidad)
            .FirstOrDefaultAsync(u => u.Correo == correo, ct);

        if (usuario is null || !BCrypt.Net.BCrypt.Verify(contrasena, usuario.HashContrasena))
        {
            throw ExcepcionAutenticacion.NoAutorizado("Invalid email or password.", "invalid_credentials");
        }

        AsegurarUsuarioPuedeAutenticarse(usuario);

        return await EmitirTokensAsync(usuario, ct);
    }

    public async Task<RespuestaAutenticacion> RefrescarAsync(
        SolicitudRefresco solicitud,
        CancellationToken ct = default)
    {
        var tokenPlano = (solicitud.TokenRefresco ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(tokenPlano))
        {
            throw ExcepcionAutenticacion.Validacion("Refresh token is required.");
        }

        var hash = HashearToken(tokenPlano);
        var registro = await db.TokensRefresco
            .Include(t => t.Usuario)
            .ThenInclude(u => u.Universidad)
            .FirstOrDefaultAsync(t => t.HashToken == hash, ct);

        if (registro is null)
        {
            throw ExcepcionAutenticacion.NoAutorizado("Invalid refresh token.", "invalid_refresh_token");
        }

        if (registro.RevocadoEn is not null)
        {
            throw ExcepcionAutenticacion.NoAutorizado("Refresh token has been revoked.", "refresh_token_revoked");
        }

        if (registro.ExpiraEn <= DateTimeOffset.UtcNow)
        {
            throw ExcepcionAutenticacion.NoAutorizado("Refresh token has expired.", "refresh_token_expired");
        }

        AsegurarUsuarioPuedeAutenticarse(registro.Usuario);

        registro.RevocadoEn = DateTimeOffset.UtcNow;
        return await EmitirTokensAsync(registro.Usuario, ct);
    }

    public async Task CerrarSesionAsync(
        Guid usuarioId,
        SolicitudCierreSesion solicitud,
        CancellationToken ct = default)
    {
        var tokenPlano = (solicitud.TokenRefresco ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(tokenPlano))
        {
            var ahora = DateTimeOffset.UtcNow;
            var tokens = await db.TokensRefresco
                .Where(t => t.UsuarioId == usuarioId && t.RevocadoEn == null)
                .ToListAsync(ct);
            foreach (var t in tokens)
            {
                t.RevocadoEn = ahora;
            }
        }
        else
        {
            var hash = HashearToken(tokenPlano);
            var registro = await db.TokensRefresco
                .FirstOrDefaultAsync(t => t.UsuarioId == usuarioId && t.HashToken == hash, ct);
            if (registro is not null && registro.RevocadoEn is null)
            {
                registro.RevocadoEn = DateTimeOffset.UtcNow;
            }
        }

        await db.SaveChangesAsync(ct);
        cacheEstado.Invalidar(usuarioId);
    }

    public async Task CambiarContrasenaAsync(
        Guid usuarioId,
        SolicitudCambioContrasena solicitud,
        CancellationToken ct = default)
    {
        var actual = solicitud.ContrasenaActual ?? string.Empty;
        var nueva = solicitud.ContrasenaNueva ?? string.Empty;

        if (string.IsNullOrWhiteSpace(actual) || string.IsNullOrWhiteSpace(nueva))
        {
            throw ExcepcionAutenticacion.Validacion("Current and new password are required.");
        }

        if (nueva.Length < 8)
        {
            throw ExcepcionAutenticacion.Validacion("New password must be at least 8 characters.");
        }

        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct)
            ?? throw ExcepcionAutenticacion.NoAutorizado("User not found.");

        if (!BCrypt.Net.BCrypt.Verify(actual, usuario.HashContrasena))
        {
            throw ExcepcionAutenticacion.NoAutorizado("Current password is incorrect.", "invalid_password");
        }

        usuario.HashContrasena = BCrypt.Net.BCrypt.HashPassword(nueva);
        usuario.DebeCambiarContrasena = false;
        usuario.ActualizadoEn = DateTimeOffset.UtcNow;

        var ahora = DateTimeOffset.UtcNow;
        var tokens = await db.TokensRefresco
            .Where(t => t.UsuarioId == usuarioId && t.RevocadoEn == null)
            .ToListAsync(ct);
        foreach (var t in tokens)
        {
            t.RevocadoEn = ahora;
        }

        await db.SaveChangesAsync(ct);
        cacheEstado.Invalidar(usuarioId);
    }

    public async Task<ResumenUsuarioDto> ObtenerPerfilAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var usuario = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == usuarioId, ct)
            ?? throw ExcepcionAutenticacion.NoAutorizado("User not found.");

        return MapearUsuario(usuario, incluirExtras: true);
    }

    private async Task<RespuestaAutenticacion> EmitirTokensAsync(Usuario usuario, CancellationToken ct)
    {
        var access = tokensJwt.CrearTokenAcceso(usuario);
        var refreshPlano = GenerarTokenRefresco();
        var refreshHash = HashearToken(refreshPlano);

        db.TokensRefresco.Add(new TokenRefresco
        {
            UsuarioId = usuario.Id,
            HashToken = refreshHash,
            ExpiraEn = DateTimeOffset.UtcNow.AddDays(_jwt.RefreshTokenDays),
            CreadoEn = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(ct);
        cacheEstado.Invalidar(usuario.Id);

        return new RespuestaAutenticacion
        {
            TokenAcceso = access,
            TokenRefresco = refreshPlano,
            ExpiraEnSegundos = tokensJwt.MinutosExpiracionAcceso * 60,
            DebeCambiarContrasena = usuario.DebeCambiarContrasena,
            Usuario = MapearUsuario(usuario, incluirExtras: false)
        };
    }

    private static void AsegurarUsuarioPuedeAutenticarse(Usuario usuario)
    {
        if (usuario.Estado == EstadoUsuario.Bloqueado)
        {
            throw ExcepcionAutenticacion.Prohibido("User is blocked.", "user_blocked");
        }

        if (usuario.Estado == EstadoUsuario.Pendiente)
        {
            throw ExcepcionAutenticacion.Prohibido("User is pending approval.", "user_pending");
        }

        if (usuario.Estado == EstadoUsuario.Eliminado)
        {
            throw ExcepcionAutenticacion.Prohibido("User is deleted.", "user_deleted");
        }

        if (usuario.Universidad is { Estado: EstadoUniversidad.Suspendida })
        {
            throw ExcepcionAutenticacion.Prohibido("University is suspended.", "university_suspended");
        }
    }

    private static ResumenUsuarioDto MapearUsuario(Usuario usuario, bool incluirExtras) => new()
    {
        Id = usuario.Id,
        Correo = usuario.Correo,
        Nombre = usuario.Nombre,
        Rol = ConversorEnumDominio.ACadenaDb(usuario.Rol),
        Genero = usuario.Genero.HasValue
            ? ConversorEnumDominio.ACadenaDb(usuario.Genero.Value)
            : null,
        ImagenPerfil = usuario.ImagenPerfil,
        UniversidadId = usuario.UniversidadId,
        CampusId = usuario.CampusId,
        DebeCambiarContrasena = incluirExtras ? usuario.DebeCambiarContrasena : null,
        Estado = incluirExtras ? ConversorEnumDominio.ACadenaDb(usuario.Estado) : null
    };

    private static string GenerarTokenRefresco()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    internal static string HashearToken(string tokenPlano)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(tokenPlano));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
