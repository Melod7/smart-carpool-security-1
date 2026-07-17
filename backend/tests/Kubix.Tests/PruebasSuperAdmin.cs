using System.Security.Claims;
using Kubix.Application.Auth;
using Kubix.Application.SuperAdmin;
using Kubix.Application.Tenancy;
using Kubix.Domain.Entities;
using Kubix.Domain.Enums;
using Kubix.Infrastructure.Auth;
using Kubix.Infrastructure.Persistence;
using Kubix.Infrastructure.Seeding;
using Kubix.Infrastructure.SuperAdmin;
using Kubix.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Kubix.Tests;

public class PruebasSuperAdmin
{
    [Fact]
    public async Task Crear_universidad_aparece_en_lista_con_configuracion()
    {
        await using var db = CrearDb();
        var servicio = CrearServicio(db);

        var creada = await servicio.CrearUniversidadAsync(new SolicitudCrearUniversidad
        {
            Nombre = "Universidad Demo",
            Slug = "demo-uni",
            DominioCorreoPermitido = "demo.edu.ec"
        });

        Assert.Equal("Universidad Demo", creada.Nombre);
        Assert.Equal("demo-uni", creada.Slug);
        Assert.Equal("active", creada.Estado);
        Assert.Equal("demo.edu.ec", creada.DominioCorreoPermitido);

        var lista = await servicio.ListarUniversidadesAsync();
        var fila = Assert.Single(lista, u => u.Slug == "demo-uni");
        Assert.Equal(0, fila.CantidadCampuses);
        Assert.Equal(0, fila.CantidadUsuarios);

        var config = await db.ConfiguracionesUniversidad.SingleAsync(c => c.UniversidadId == creada.Id);
        Assert.Equal("demo.edu.ec", config.DominioCorreoPermitido);
        Assert.Equal("America/Guayaquil", config.ZonaHoraria);
        Assert.True(config.GamificacionHabilitada);

        var auditoria = await db.EventosAuditoria.SingleAsync(e => e.Accion == "university.created");
        Assert.Equal(creada.Id, auditoria.UniversidadId);
    }

    [Fact]
    public async Task Suspender_universidad_bloquea_login_con_403()
    {
        await using var db = await CrearDbConSeedAsync();
        var servicio = CrearServicio(db);
        var auth = CrearAuth(db);

        var uni = await db.Universidades.SingleAsync(u => u.Slug == "utn");
        await servicio.SuspenderUniversidadAsync(uni.Id);

        await db.Entry(uni).ReloadAsync();
        Assert.Equal(EstadoUniversidad.Suspendida, uni.Estado);

        var ex = await Assert.ThrowsAsync<ExcepcionAutenticacion>(() =>
            auth.IniciarSesionAsync(new SolicitudInicioSesion
            {
                Correo = "driver1@utn.local",
                Contrasena = SembradorBaseDatos.ContrasenaPorDefecto
            }));

        Assert.Equal(403, ex.CodigoEstado);
        Assert.Equal("university_suspended", ex.Codigo);
    }

    [Fact]
    public async Task Crear_coordinador_devuelve_temporaryPassword_y_login_exige_cambio()
    {
        await using var db = await CrearDbConSeedAsync();
        var servicio = CrearServicio(db);
        var auth = CrearAuth(db);

        var uni = await db.Universidades.SingleAsync(u => u.Slug == "utn");
        var creado = await servicio.CrearCoordinadorAsync(uni.Id, new SolicitudCrearCoordinador
        {
            Nombre = "Nuevo Coordinador",
            Correo = "nuevo.coord@utn.local"
        });

        Assert.False(string.IsNullOrWhiteSpace(creado.ContrasenaTemporal));
        Assert.True(creado.DebeCambiarContrasena);
        Assert.Equal(uni.Id, creado.UniversidadId);

        var login = await auth.IniciarSesionAsync(new SolicitudInicioSesion
        {
            Correo = "nuevo.coord@utn.local",
            Contrasena = creado.ContrasenaTemporal
        });

        Assert.True(login.DebeCambiarContrasena);
        Assert.Equal("coordinador", login.Usuario.Rol);
    }

    [Fact]
    public async Task Eliminar_coordinador_anonimiza_revoca_sesiones_y_lo_oculta()
    {
        await using var db = await CrearDbConSeedAsync();
        var servicio = CrearServicio(db);
        var auth = CrearAuth(db);

        var uni = await db.Universidades.SingleAsync(u => u.Slug == "utn");
        var creado = await servicio.CrearCoordinadorAsync(uni.Id, new SolicitudCrearCoordinador
        {
            Nombre = "Coordinador Eliminable",
            Correo = "eliminable.coord@utn.local"
        });

        await auth.IniciarSesionAsync(new SolicitudInicioSesion
        {
            Correo = creado.Correo,
            Contrasena = creado.ContrasenaTemporal
        });

        await servicio.EliminarCoordinadorAsync(creado.Id);

        var eliminado = await db.Usuarios.SingleAsync(u => u.Id == creado.Id);
        Assert.Equal(EstadoUsuario.Eliminado, eliminado.Estado);
        Assert.Equal("Deleted coordinator", eliminado.Nombre);
        Assert.StartsWith("deleted-coordinator-", eliminado.Correo);
        Assert.False(eliminado.DebeCambiarContrasena);

        var tokens = await db.TokensRefresco.Where(t => t.UsuarioId == creado.Id).ToListAsync();
        Assert.NotEmpty(tokens);
        Assert.All(tokens, token => Assert.NotNull(token.RevocadoEn));

        var coordinadores = await servicio.ListarCoordinadoresAsync(uni.Id);
        Assert.DoesNotContain(coordinadores, c => c.Id == creado.Id);

        var evento = await db.EventosAuditoria.SingleAsync(
            e => e.Accion == "coordinador.deleted" && e.UsuarioId == creado.Id);
        Assert.Equal(uni.Id, evento.UniversidadId);
    }

    [Fact]
    public async Task Stats_coinciden_tras_crear_entidades()
    {
        await using var db = CrearDb();
        var servicio = CrearServicio(db);

        var statsVacias = await servicio.ObtenerStatsAsync();
        Assert.Equal(0, statsVacias.CantidadUniversidades);
        Assert.Equal(0, statsVacias.TotalUsuarios);
        Assert.Equal(0, statsVacias.ViajesHoy);
        Assert.Equal(0, statsVacias.CantidadSosActivos);

        var uni = await servicio.CrearUniversidadAsync(new SolicitudCrearUniversidad
        {
            Nombre = "Stats Uni",
            Slug = "stats-uni"
        });

        var campus = await servicio.CrearCampusAsync(uni.Id, new SolicitudCrearCampus
        {
            Nombre = "Campus Central",
            Direccion = "Calle 1",
            Lat = -0.2,
            Lng = -78.5
        });

        await servicio.CrearCoordinadorAsync(uni.Id, new SolicitudCrearCoordinador
        {
            Nombre = "Coord Stats",
            Correo = "coord@stats.local"
        });

        var ahora = DateTimeOffset.UtcNow;
        var conductor = new Usuario
        {
            UniversidadId = uni.Id,
            CampusId = campus.Id,
            Rol = RolUsuario.Conductor,
            Estado = EstadoUsuario.Activo,
            Nombre = "Conductor Stats",
            Correo = "driver@stats.local",
            HashContrasena = BCrypt.Net.BCrypt.HashPassword("ChangeMe123!"),
            CreadoEn = ahora,
            ActualizadoEn = ahora
        };
        db.Usuarios.Add(conductor);
        await db.SaveChangesAsync();

        db.Viajes.Add(new Viaje
        {
            UniversidadId = uni.Id,
            ConductorId = conductor.Id,
            CampusDestinoId = campus.Id,
            OrigenTexto = "Origen",
            OrigenLat = -0.21,
            OrigenLng = -78.51,
            SaleEn = ahora,
            AsientosDisponibles = 2,
            DistanciaKm = 5,
            Estado = EstadoViaje.Programado,
            CreadoEn = ahora,
            ActualizadoEn = ahora
        });
        db.AlertasSos.Add(new AlertaSos
        {
            UniversidadId = uni.Id,
            UsuarioId = conductor.Id,
            Lat = -0.21,
            Lng = -78.51,
            DisparadaEn = ahora,
            Estado = EstadoAlertaSos.Activa,
            CreadoEn = ahora,
            ActualizadoEn = ahora
        });
        await db.SaveChangesAsync();

        var stats = await servicio.ObtenerStatsAsync();
        Assert.Equal(1, stats.CantidadUniversidades);
        Assert.Equal(2, stats.TotalUsuarios);
        Assert.Equal(1, stats.ViajesHoy);
        Assert.Equal(1, stats.CantidadSosActivos);
    }

    [Fact]
    public async Task Politica_SoloSuperAdmin_falla_para_conductor()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AgregarTenancy();
        await using var sp = services.BuildServiceProvider();

        var authz = sp.GetRequiredService<IAuthorizationService>();
        var conductor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("role", "driver"), new Claim(ClaimTypes.Role, "driver")],
            authenticationType: "Test"));

        var resultado = await authz.AuthorizeAsync(conductor, resource: null, NombresPoliticas.SoloSuperAdmin);
        Assert.False(resultado.Succeeded);
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
        await sembrador.SembrarDemoAsync();
        return db;
    }

    private static ContextoApp CrearDb()
    {
        var opciones = new DbContextOptionsBuilder<ContextoApp>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ContextoApp(opciones, new ContextoInquilino { OmitirFiltros = true });
    }

    private static IServicioSuperAdmin CrearServicio(ContextoApp db)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var cacheEstado = new CacheEstadoUsuario(db, cache);
        var auditoria = new EscritorAuditoria(db, new ContextoInquilino { OmitirFiltros = true });
        return new ServicioSuperAdmin(db, auditoria, cacheEstado);
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
