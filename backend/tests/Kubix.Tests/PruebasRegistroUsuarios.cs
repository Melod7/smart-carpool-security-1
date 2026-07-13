using Kubix.Application.Auth;
using Kubix.Application.Usuarios;
using Kubix.Domain.Enums;
using Kubix.Infrastructure.Auth;
using Kubix.Infrastructure.Persistence;
using Kubix.Infrastructure.Seeding;
using Kubix.Infrastructure.Tenancy;
using Kubix.Infrastructure.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Kubix.Tests;

public class PruebasRegistroUsuarios
{
    [Fact]
    public async Task Registrar_dominio_incorrecto_devuelve_422()
    {
        await using var db = await CrearDbConSeedAsync();
        var servicio = CrearServicio(db, OmitirFiltros: true);

        var uni = await db.Universidades.SingleAsync(u => u.Slug == "utn");
        var campus = await db.Sedes.FirstAsync(c => c.UniversidadId == uni.Id);

        var ex = await Assert.ThrowsAsync<ExcepcionRegistroUsuarios>(() =>
            servicio.RegistrarAsync(new SolicitudRegistroDto
            {
                UniversidadId = uni.Id,
                CampusId = campus.Id,
                Rol = "passenger",
                Nombre = "Fuera Dominio",
                Correo = "alguien@gmail.com",
                Contrasena = "ChangeMe123!",
                Carrera = "Software"
            }));

        Assert.Equal(422, ex.CodigoEstado);
        Assert.Equal("invalid_email_domain", ex.Codigo);
        Assert.Contains("utn.edu.ec", ex.Message);
    }

    [Fact]
    public async Task Aceptar_crea_usuario_capaz_de_iniciar_sesion()
    {
        await using var db = await CrearDbConSeedAsync();
        var uni = await db.Universidades.SingleAsync(u => u.Slug == "utn");
        var campus = await db.Sedes.FirstAsync(c => c.UniversidadId == uni.Id && c.Nombre.Contains("Ibarra"));
        var coord = await db.Usuarios.SingleAsync(u => u.Correo == "coordinador@utn.local");

        var publico = CrearServicio(db, OmitirFiltros: true);
        var registro = await publico.RegistrarAsync(new SolicitudRegistroDto
        {
            UniversidadId = uni.Id,
            CampusId = campus.Id,
            Rol = "driver",
            Nombre = "Nuevo Driver QA",
            Correo = "nuevo.qa@utn.edu.ec",
            Contrasena = "Secreta123!",
            Carrera = "Software",
            NumeroIdentificacion = "5555",
            Vehiculo = new VehiculoRegistroDto
            {
                MarcaModelo = "Chevrolet Spark",
                Placa = "PBA-5555",
                Color = "Azul",
                AsientosTotales = 3
            }
        });

        var admin = CrearServicio(db, OmitirFiltros: false, universidadId: uni.Id, usuarioId: coord.Id);
        await admin.AceptarSolicitudAsync(registro.Id);

        var usuario = await db.Usuarios.SingleAsync(u => u.Correo == "nuevo.qa@utn.edu.ec");
        Assert.Equal(EstadoUsuario.Activo, usuario.Estado);
        Assert.Equal(RolUsuario.Conductor, usuario.Rol);
        Assert.Equal(1, await db.Vehiculos.CountAsync(v => v.UsuarioId == usuario.Id));

        var auth = CrearAuth(db);
        var login = await auth.IniciarSesionAsync(new SolicitudInicioSesion
        {
            Correo = "nuevo.qa@utn.edu.ec",
            Contrasena = "Secreta123!"
        });

        Assert.Equal("driver", login.Usuario.Rol);
        Assert.False(string.IsNullOrWhiteSpace(login.TokenAcceso));
    }

    [Fact]
    public async Task Denegar_mantiene_incapaz_de_iniciar_sesion()
    {
        await using var db = await CrearDbConSeedAsync();
        var uni = await db.Universidades.SingleAsync(u => u.Slug == "utn");
        var campus = await db.Sedes.FirstAsync(c => c.UniversidadId == uni.Id);
        var coord = await db.Usuarios.SingleAsync(u => u.Correo == "coordinador@utn.local");

        var publico = CrearServicio(db, OmitirFiltros: true);
        var registro = await publico.RegistrarAsync(new SolicitudRegistroDto
        {
            UniversidadId = uni.Id,
            CampusId = campus.Id,
            Rol = "passenger",
            Nombre = "Denegado QA",
            Correo = "denegado.qa@utn.edu.ec",
            Contrasena = "Secreta123!",
            Carrera = "Civil"
        });

        var admin = CrearServicio(db, OmitirFiltros: false, universidadId: uni.Id, usuarioId: coord.Id);
        await admin.DenegarSolicitudAsync(registro.Id);

        Assert.False(await db.Usuarios.AnyAsync(u => u.Correo == "denegado.qa@utn.edu.ec"));
        var solicitud = await db.SolicitudesRegistro.SingleAsync(s => s.Id == registro.Id);
        Assert.Equal(EstadoSolicitudRegistro.Denegada, solicitud.Estado);

        var auth = CrearAuth(db);
        var ex = await Assert.ThrowsAsync<ExcepcionAutenticacion>(() =>
            auth.IniciarSesionAsync(new SolicitudInicioSesion
            {
                Correo = "denegado.qa@utn.edu.ec",
                Contrasena = "Secreta123!"
            }));
        Assert.Equal(401, ex.CodigoEstado);
        Assert.Equal("invalid_credentials", ex.Codigo);
    }

    [Fact]
    public async Task Bloquear_invalida_cache_y_falla_login()
    {
        await using var db = await CrearDbConSeedAsync();
        var uni = await db.Universidades.SingleAsync(u => u.Slug == "utn");
        var coord = await db.Usuarios.SingleAsync(u => u.Correo == "coordinador@utn.local");
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver1@utn.local");

        var cache = new MemoryCache(new MemoryCacheOptions());
        var cacheEstado = new CacheEstadoUsuario(db, cache);
        var auth = CrearAuth(db, cacheEstado);

        var login = await auth.IniciarSesionAsync(new SolicitudInicioSesion
        {
            Correo = "driver1@utn.local",
            Contrasena = SembradorBaseDatos.ContrasenaPorDefecto
        });
        Assert.False(string.IsNullOrWhiteSpace(login.TokenAcceso));

        var antes = await cacheEstado.ObtenerAsync(conductor.Id);
        Assert.True(antes.Permitido);

        var admin = CrearServicio(
            db,
            OmitirFiltros: false,
            universidadId: uni.Id,
            usuarioId: coord.Id,
            cacheEstado: cacheEstado);
        await admin.BloquearUsuarioAsync(conductor.Id);

        await db.Entry(conductor).ReloadAsync();
        Assert.Equal(EstadoUsuario.Bloqueado, conductor.Estado);

        var despues = await cacheEstado.ObtenerAsync(conductor.Id);
        Assert.False(despues.Permitido);
        Assert.Equal("blocked", despues.EstadoUsuario);

        var tokensActivos = await db.TokensRefresco.CountAsync(t =>
            t.UsuarioId == conductor.Id && t.RevocadoEn == null);
        Assert.Equal(0, tokensActivos);

        var ex = await Assert.ThrowsAsync<ExcepcionAutenticacion>(() =>
            auth.IniciarSesionAsync(new SolicitudInicioSesion
            {
                Correo = "driver1@utn.local",
                Contrasena = SembradorBaseDatos.ContrasenaPorDefecto
            }));
        Assert.Equal(403, ex.CodigoEstado);
        Assert.Equal("user_blocked", ex.Codigo);
    }

    [Fact]
    public async Task Contactos_emergencia_maximo_3_el_cuarto_422()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver1@utn.local");
        var servicio = CrearServicio(
            db,
            OmitirFiltros: false,
            universidadId: conductor.UniversidadId,
            usuarioId: conductor.Id);

        for (var i = 1; i <= 3; i++)
        {
            var creado = await servicio.CrearContactoEmergenciaAsync(
                conductor.Id,
                new SolicitudCrearContactoEmergencia
                {
                    Nombre = $"Contacto {i}",
                    Relacion = "familiar",
                    Telefono = $"+59399000000{i}"
                });
            Assert.Equal($"Contacto {i}", creado.Nombre);
        }

        var lista = await servicio.ListarContactosEmergenciaAsync(conductor.Id);
        Assert.Equal(3, lista.Count);

        var ex = await Assert.ThrowsAsync<ExcepcionRegistroUsuarios>(() =>
            servicio.CrearContactoEmergenciaAsync(
                conductor.Id,
                new SolicitudCrearContactoEmergencia
                {
                    Nombre = "Contacto 4",
                    Relacion = "amigo",
                    Telefono = "+593990000004"
                }));

        Assert.Equal(422, ex.CodigoEstado);
        Assert.Equal("max_emergency_contacts", ex.Codigo);
    }

    [Fact]
    public async Task Universidades_publicas_listan_campuses_sin_auth()
    {
        await using var db = await CrearDbConSeedAsync();
        var servicio = CrearServicio(db, OmitirFiltros: true);

        var lista = await servicio.ListarUniversidadesPublicasAsync();

        Assert.Equal(2, lista.Count);
        var utn = Assert.Single(lista, u => u.Nombre.Contains("Técnica del Norte"));
        Assert.Equal(2, utn.Campuses.Count);
        Assert.Contains(utn.Campuses, c => c.Nombre.Contains("Ibarra"));
        Assert.Contains(utn.Campuses, c => c.Nombre.Contains("Otavalo"));

        var puce = Assert.Single(lista, u => u.Nombre.Contains("Católica"));
        Assert.Equal(2, puce.Campuses.Count);
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

    private static ContextoApp CrearDb(ContextoInquilino? inquilino = null)
    {
        var opciones = new DbContextOptionsBuilder<ContextoApp>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ContextoApp(opciones, inquilino ?? new ContextoInquilino { OmitirFiltros = true });
    }

    private static IServicioRegistroUsuarios CrearServicio(
        ContextoApp db,
        bool OmitirFiltros,
        Guid? universidadId = null,
        Guid? usuarioId = null,
        ICacheEstadoUsuario? cacheEstado = null)
    {
        var inquilino = new ContextoInquilino
        {
            OmitirFiltros = OmitirFiltros,
            UniversidadId = universidadId,
            UsuarioId = usuarioId,
            Rol = universidadId is null ? null : RolUsuario.Coordinador
        };
        var auditoria = new EscritorAuditoria(db, inquilino);
        cacheEstado ??= new CacheEstadoUsuario(db, new MemoryCache(new MemoryCacheOptions()));
        return new ServicioRegistroUsuarios(db, inquilino, auditoria, cacheEstado);
    }

    private static IServicioAutenticacion CrearAuth(ContextoApp db, ICacheEstadoUsuario? cacheEstado = null)
    {
        var jwtOptions = Options.Create(new OpcionesJwt
        {
            Issuer = "kubix",
            Audience = "kubix",
            Key = "dev-only-change-me-to-a-long-secret-key-32+",
            AccessTokenMinutes = 60,
            RefreshTokenDays = 14
        });
        cacheEstado ??= new CacheEstadoUsuario(db, new MemoryCache(new MemoryCacheOptions()));
        var tokens = new ServicioTokenJwt(jwtOptions);
        return new ServicioAutenticacion(db, tokens, cacheEstado, jwtOptions);
    }
}
