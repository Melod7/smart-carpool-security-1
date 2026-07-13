using System.IdentityModel.Tokens.Jwt;
using Kubix.Application.Auth;
using Kubix.Domain.Enums;
using Kubix.Infrastructure.Auth;
using Kubix.Infrastructure.Persistence;
using Kubix.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Kubix.Tests;

public class PruebasAutenticacion
{
    [Fact]
    public async Task Login_exitoso_para_usuario_seedeado()
    {
        await using var db = await CrearDbConSeedAsync();
        var auth = CrearAuth(db);

        var respuesta = await auth.IniciarSesionAsync(new SolicitudInicioSesion
        {
            Correo = "driver1@utn.local",
            Contrasena = SembradorBaseDatos.ContrasenaPorDefecto
        });

        Assert.False(string.IsNullOrWhiteSpace(respuesta.TokenAcceso));
        Assert.False(string.IsNullOrWhiteSpace(respuesta.TokenRefresco));
        Assert.Equal(3600, respuesta.ExpiraEnSegundos);
        Assert.False(respuesta.DebeCambiarContrasena);
        Assert.Equal("driver", respuesta.Usuario.Rol);
        Assert.Equal("driver1@utn.local", respuesta.Usuario.Correo);
        Assert.NotNull(respuesta.Usuario.UniversidadId);
        Assert.NotNull(respuesta.Usuario.CampusId);
    }

    [Fact]
    public async Task Login_rechaza_usuarios_pendientes_y_bloqueados()
    {
        await using var db = await CrearDbConSeedAsync();
        var auth = CrearAuth(db);

        var pendiente = await Assert.ThrowsAsync<ExcepcionAutenticacion>(() =>
            auth.IniciarSesionAsync(new SolicitudInicioSesion
            {
                Correo = "pending@utn.local",
                Contrasena = SembradorBaseDatos.ContrasenaPorDefecto
            }));
        Assert.Equal(403, pendiente.CodigoEstado);
        Assert.Equal("user_pending", pendiente.Codigo);

        var bloqueado = await db.Usuarios.SingleAsync(u => u.Correo == "pax2@utn.local");
        bloqueado.Estado = EstadoUsuario.Bloqueado;
        await db.SaveChangesAsync();

        var exBloqueo = await Assert.ThrowsAsync<ExcepcionAutenticacion>(() =>
            auth.IniciarSesionAsync(new SolicitudInicioSesion
            {
                Correo = "pax2@utn.local",
                Contrasena = SembradorBaseDatos.ContrasenaPorDefecto
            }));
        Assert.Equal(403, exBloqueo.CodigoEstado);
        Assert.Equal("user_blocked", exBloqueo.Codigo);
    }

    [Fact]
    public async Task Jwt_incluye_claim_role_en_snake_case()
    {
        await using var db = await CrearDbConSeedAsync();
        var auth = CrearAuth(db);

        var respuesta = await auth.IniciarSesionAsync(new SolicitudInicioSesion
        {
            Correo = "driver1@utn.local",
            Contrasena = SembradorBaseDatos.ContrasenaPorDefecto
        });

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(respuesta.TokenAcceso);

        Assert.Equal("driver", jwt.Claims.Single(c => c.Type == "role").Value);
        Assert.False(string.IsNullOrWhiteSpace(jwt.Claims.Single(c => c.Type == "sub").Value));
        Assert.False(string.IsNullOrWhiteSpace(jwt.Claims.Single(c => c.Type == "university_id").Value));
        Assert.False(string.IsNullOrWhiteSpace(jwt.Claims.Single(c => c.Type == "campus_id").Value));

        var super = await auth.IniciarSesionAsync(new SolicitudInicioSesion
        {
            Correo = "superadmin@kubix.local",
            Contrasena = SembradorBaseDatos.ContrasenaPorDefecto
        });
        var jwtSuper = handler.ReadJwtToken(super.TokenAcceso);
        Assert.Equal("super_admin", jwtSuper.Claims.Single(c => c.Type == "role").Value);
        Assert.DoesNotContain(jwtSuper.Claims, c => c.Type == "university_id");
    }

    [Fact]
    public async Task Refresh_rota_token_y_el_viejo_falla()
    {
        await using var db = await CrearDbConSeedAsync();
        var auth = CrearAuth(db);

        var login = await auth.IniciarSesionAsync(new SolicitudInicioSesion
        {
            Correo = "driver1@utn.local",
            Contrasena = SembradorBaseDatos.ContrasenaPorDefecto
        });

        var refresco = await auth.RefrescarAsync(new SolicitudRefresco
        {
            TokenRefresco = login.TokenRefresco
        });

        Assert.NotEqual(login.TokenRefresco, refresco.TokenRefresco);
        Assert.False(string.IsNullOrWhiteSpace(refresco.TokenAcceso));

        var reuso = await Assert.ThrowsAsync<ExcepcionAutenticacion>(() =>
            auth.RefrescarAsync(new SolicitudRefresco { TokenRefresco = login.TokenRefresco }));
        Assert.Equal(401, reuso.CodigoEstado);
        Assert.Equal("refresh_token_revoked", reuso.Codigo);
    }

    [Fact]
    public async Task Cambio_contrasena_limpia_flag_must_change_password()
    {
        await using var db = await CrearDbConSeedAsync();
        var auth = CrearAuth(db);

        var login = await auth.IniciarSesionAsync(new SolicitudInicioSesion
        {
            Correo = "coordinador@utn.local",
            Contrasena = SembradorBaseDatos.ContrasenaPorDefecto
        });
        Assert.True(login.DebeCambiarContrasena);

        var usuario = await db.Usuarios.SingleAsync(u => u.Correo == "coordinador@utn.local");
        await auth.CambiarContrasenaAsync(usuario.Id, new SolicitudCambioContrasena
        {
            ContrasenaActual = SembradorBaseDatos.ContrasenaPorDefecto,
            ContrasenaNueva = "NuevaSegura123!"
        });

        await db.Entry(usuario).ReloadAsync();
        Assert.False(usuario.DebeCambiarContrasena);
        Assert.True(BCrypt.Net.BCrypt.Verify("NuevaSegura123!", usuario.HashContrasena));

        var tokensActivos = await db.TokensRefresco.CountAsync(t =>
            t.UsuarioId == usuario.Id && t.RevocadoEn == null);
        Assert.Equal(0, tokensActivos);

        var nuevoLogin = await auth.IniciarSesionAsync(new SolicitudInicioSesion
        {
            Correo = "coordinador@utn.local",
            Contrasena = "NuevaSegura123!"
        });
        Assert.False(nuevoLogin.DebeCambiarContrasena);
    }

    [Fact]
    public async Task Login_rechaza_universidad_suspendida()
    {
        await using var db = await CrearDbConSeedAsync();
        var auth = CrearAuth(db);

        var uni = await db.Universidades.SingleAsync(u => u.Slug == "utn");
        uni.Estado = EstadoUniversidad.Suspendida;
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ExcepcionAutenticacion>(() =>
            auth.IniciarSesionAsync(new SolicitudInicioSesion
            {
                Correo = "driver1@utn.local",
                Contrasena = SembradorBaseDatos.ContrasenaPorDefecto
            }));
        Assert.Equal(403, ex.CodigoEstado);
        Assert.Equal("university_suspended", ex.Codigo);
    }

    private static async Task<ContextoApp> CrearDbConSeedAsync()
    {
        var db = CrearDb();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SUPER_ADMIN_EMAIL"] = "superadmin@kubix.local",
                ["SUPER_ADMIN_PASSWORD"] = SembradorBaseDatos.ContrasenaPorDefecto
            })
            .Build();

        var sembrador = new SembradorBaseDatos(db, config, NullLogger<SembradorBaseDatos>.Instance);
        await sembrador.SembrarAsync();
        return db;
    }

    private static ContextoApp CrearDb()
    {
        var opciones = new DbContextOptionsBuilder<ContextoApp>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ContextoApp(opciones);
    }

    private static IServicioAutenticacion CrearAuth(ContextoApp db)
    {
        var jwtOptions = Options.Create(new OpcionesJwt
        {
            Issuer = "kubix",
            Audience = "kubix",
            Key = "dev-only-change-me-to-a-long-secret-key-32+",
            AccessTokenMinutes = 60,
            RefreshTokenDays = 14
        });
        var cache = new MemoryCache(new MemoryCacheOptions());
        var cacheEstado = new CacheEstadoUsuario(db, cache);
        var tokens = new ServicioTokenJwt(jwtOptions);
        return new ServicioAutenticacion(db, tokens, cacheEstado, jwtOptions);
    }
}
