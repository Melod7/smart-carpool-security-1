using Kubix.Application.Auth;
using Kubix.Application.Usuarios;
using Kubix.Domain;
using Kubix.Domain.Entities;
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
    private const string ImagenPrueba = "data:image/png;base64,AQ==";

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
                Genero = "female",
                ImagenPerfil = ImagenPrueba,
                Carrera = "Software",
                NumeroIdentificacion = "9001"
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
            Genero = "male",
            ImagenPerfil = ImagenPrueba,
            Carrera = "Software",
            NumeroIdentificacion = "5555",
            Vehiculo = new VehiculoRegistroDto
            {
                MarcaModelo = "Chevrolet Spark",
                Placa = "PBA-5555",
                Color = "Azul",
                AsientosTotales = 3,
                Imagen = ImagenPrueba
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
            Genero = "female",
            ImagenPerfil = ImagenPrueba,
            Carrera = "Civil",
            NumeroIdentificacion = "9002"
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

    [Theory]
    [InlineData(null, "gender_required")]
    [InlineData("", "gender_required")]
    [InlineData("other", "invalid_gender")]
    public async Task Registrar_rechaza_genero_ausente_o_invalido(string? genero, string codigo)
    {
        await using var db = await CrearDbConSeedAsync();
        var uni = await db.Universidades.SingleAsync(u => u.Slug == "utn");
        var campus = await db.Sedes.FirstAsync(c => c.UniversidadId == uni.Id);
        var servicio = CrearServicio(db, OmitirFiltros: true);

        var ex = await Assert.ThrowsAsync<ExcepcionRegistroUsuarios>(() =>
            servicio.RegistrarAsync(new SolicitudRegistroDto
            {
                UniversidadId = uni.Id,
                CampusId = campus.Id,
                Rol = "passenger",
                Nombre = "Género QA",
                Correo = $"gender-{Guid.NewGuid():N}@utn.edu.ec",
                Contrasena = "Secreta123!",
                Genero = genero,
                ImagenPerfil = ImagenPrueba,
                Carrera = "Software",
                NumeroIdentificacion = "9003"
            }));

        Assert.Equal(422, ex.CodigoEstado);
        Assert.Equal(codigo, ex.Codigo);
    }

    [Theory]
    [InlineData(true, false, "career_required")]
    [InlineData(false, true, "id_number_required")]
    public async Task Registrar_requiere_carrera_y_numero_identificacion(
        bool omitirCarrera,
        bool omitirIdentificacion,
        string codigo)
    {
        await using var db = await CrearDbConSeedAsync();
        var uni = await db.Universidades.SingleAsync(u => u.Slug == "utn");
        var campus = await db.Sedes.FirstAsync(c => c.UniversidadId == uni.Id);
        var servicio = CrearServicio(db, OmitirFiltros: true);

        var ex = await Assert.ThrowsAsync<ExcepcionRegistroUsuarios>(() =>
            servicio.RegistrarAsync(new SolicitudRegistroDto
            {
                UniversidadId = uni.Id,
                CampusId = campus.Id,
                Rol = "passenger",
                Nombre = "Campos obligatorios QA",
                Correo = $"required-{Guid.NewGuid():N}@utn.edu.ec",
                Contrasena = "Secreta123!",
                Genero = "female",
                ImagenPerfil = ImagenPrueba,
                Carrera = omitirCarrera ? " " : "Software",
                NumeroIdentificacion = omitirIdentificacion ? "" : "9010"
            }));

        Assert.Equal(422, ex.CodigoEstado);
        Assert.Equal(codigo, ex.Codigo);
    }

    [Fact]
    public async Task Registrar_requiere_foto_perfil_y_foto_vehiculo()
    {
        await using var db = await CrearDbConSeedAsync();
        var uni = await db.Universidades.SingleAsync(u => u.Slug == "utn");
        var campus = await db.Sedes.FirstAsync(c => c.UniversidadId == uni.Id);
        var servicio = CrearServicio(db, OmitirFiltros: true);

        var sinPerfil = await Assert.ThrowsAsync<ExcepcionRegistroUsuarios>(() =>
            servicio.RegistrarAsync(new SolicitudRegistroDto
            {
                UniversidadId = uni.Id,
                CampusId = campus.Id,
                Rol = "passenger",
                Nombre = "Sin foto",
                Correo = "sin.foto@utn.edu.ec",
                Contrasena = "Secreta123!",
                Genero = "female",
                Carrera = "Software",
                NumeroIdentificacion = "9004"
            }));
        Assert.Equal("profile_image_required", sinPerfil.Codigo);

        var sinVehiculo = await Assert.ThrowsAsync<ExcepcionRegistroUsuarios>(() =>
            servicio.RegistrarAsync(new SolicitudRegistroDto
            {
                UniversidadId = uni.Id,
                CampusId = campus.Id,
                Rol = "driver",
                Nombre = "Sin auto foto",
                Correo = "sin.auto.foto@utn.edu.ec",
                Contrasena = "Secreta123!",
                Genero = "male",
                ImagenPerfil = ImagenPrueba,
                Carrera = "Software",
                NumeroIdentificacion = "9005",
                Vehiculo = new VehiculoRegistroDto
                {
                    MarcaModelo = "Kia Rio",
                    Placa = "PBA-1010",
                    Color = "Azul",
                    AsientosTotales = 3
                }
            }));
        Assert.Equal("vehicle_image_required", sinVehiculo.Codigo);
    }

    [Fact]
    public async Task Cambio_pasajero_a_conductor_queda_pendiente_y_al_aceptar_cambia_rol()
    {
        await using var db = await CrearDbConSeedAsync();
        var pasajero = await db.Usuarios.SingleAsync(u => u.Correo == "pax2@utn.local");
        var coordinador = await db.Usuarios.SingleAsync(u => u.Correo == "coordinador@utn.local");
        var auth = CrearAuth(db);
        await auth.IniciarSesionAsync(new SolicitudInicioSesion
        {
            Correo = pasajero.Correo,
            Contrasena = SembradorBaseDatos.ContrasenaPorDefecto
        });

        var servicio = CrearServicio(
            db,
            OmitirFiltros: false,
            universidadId: pasajero.UniversidadId,
            usuarioId: pasajero.Id);
        var respuesta = await servicio.CambiarModoAsync(
            pasajero.Id,
            new SolicitudCambioModo
            {
                Modo = "driver",
                Vehiculo = new VehiculoRegistroDto
                {
                    MarcaModelo = "Mazda 2",
                    Placa = "PBA-2020",
                    Color = "Rojo",
                    AsientosTotales = 3,
                    Imagen = ImagenPrueba
                }
            });

        Assert.Equal("pending", respuesta.Estado);
        Assert.Equal("role_change_driver", respuesta.Tipo);
        Assert.Equal(RolUsuario.Pasajero, pasajero.Rol);

        var admin = CrearServicio(
            db,
            OmitirFiltros: false,
            universidadId: pasajero.UniversidadId,
            usuarioId: coordinador.Id);
        await admin.AceptarSolicitudAsync(respuesta.SolicitudId!.Value);
        await db.Entry(pasajero).ReloadAsync();

        Assert.Equal(RolUsuario.Conductor, pasajero.Rol);
        var vehiculo = await db.Vehiculos.SingleAsync(v => v.UsuarioId == pasajero.Id);
        Assert.Equal(ImagenPrueba, vehiculo.Imagen);
        Assert.Equal(0, await db.TokensRefresco.CountAsync(
            t => t.UsuarioId == pasajero.Id && t.RevocadoEn == null));
    }

    [Fact]
    public async Task Cambio_conductor_a_pasajero_es_inmediato_sin_viajes_activos()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver3@utn.local");
        var auth = CrearAuth(db);
        await auth.IniciarSesionAsync(new SolicitudInicioSesion
        {
            Correo = conductor.Correo,
            Contrasena = SembradorBaseDatos.ContrasenaPorDefecto
        });

        var servicio = CrearServicio(
            db,
            OmitirFiltros: false,
            universidadId: conductor.UniversidadId,
            usuarioId: conductor.Id);
        var respuesta = await servicio.CambiarModoAsync(
            conductor.Id,
            new SolicitudCambioModo { Modo = "passenger" });

        await db.Entry(conductor).ReloadAsync();
        Assert.Equal("changed", respuesta.Estado);
        Assert.True(respuesta.RequiereReautenticacion);
        Assert.Equal(RolUsuario.Pasajero, conductor.Rol);
        Assert.Equal(0, await db.TokensRefresco.CountAsync(
            t => t.UsuarioId == conductor.Id && t.RevocadoEn == null));
    }

    [Fact]
    public async Task Cambio_conductor_a_pasajero_rechaza_viaje_programado()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver1@utn.local");
        var servicio = CrearServicio(
            db,
            OmitirFiltros: false,
            universidadId: conductor.UniversidadId,
            usuarioId: conductor.Id);

        var ex = await Assert.ThrowsAsync<ExcepcionRegistroUsuarios>(() =>
            servicio.CambiarModoAsync(
                conductor.Id,
                new SolicitudCambioModo { Modo = "passenger" }));

        Assert.Equal(409, ex.CodigoEstado);
        Assert.Equal("active_trip_blocks_mode_change", ex.Codigo);
    }

    [Fact]
    public async Task Eliminar_usuario_crea_tombstone_cancela_pendientes_y_lo_excluye_de_admin()
    {
        await using var db = await CrearDbConSeedAsync();
        var uni = await db.Universidades.SingleAsync(u => u.Slug == "utn");
        var campus = await db.Sedes.FirstAsync(c => c.UniversidadId == uni.Id);
        var coordinador = await db.Usuarios.SingleAsync(u => u.Correo == "coordinador@utn.local");
        var otroConductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver3@utn.local");
        var pasajeroExterno = await db.Usuarios.SingleAsync(u => u.Correo == "pax3@utn.local");
        const string contrasena = "Eliminar123!";
        var ahora = DateTimeOffset.UtcNow;
        var usuario = new Usuario
        {
            UniversidadId = uni.Id,
            CampusId = campus.Id,
            Rol = RolUsuario.Conductor,
            Estado = EstadoUsuario.Activo,
            Nombre = "Persona a eliminar",
            Correo = "delete-me@utn.local",
            HashContrasena = BCrypt.Net.BCrypt.HashPassword(contrasena),
            Genero = GeneroUsuario.Masculino,
            ImagenPerfil = ImagenPrueba,
            Carrera = "Software",
            NumeroIdentificacion = "DELETE-01"
        };
        var viajePropio = NuevoViaje(usuario, campus, EstadoViaje.Programado, ahora.AddHours(2), 2);
        var viajeAjeno = NuevoViaje(otroConductor, campus, EstadoViaje.Programado, ahora.AddHours(3), 1);
        var solicitudViajePropio = new SolicitudViaje
        {
            UniversidadId = uni.Id,
            ViajeId = viajePropio.Id,
            PasajeroId = pasajeroExterno.Id,
            RecogidaTexto = "Punto A",
            Estado = EstadoSolicitudViaje.Aceptada
        };
        var solicitudComoPasajero = new SolicitudViaje
        {
            UniversidadId = uni.Id,
            ViajeId = viajeAjeno.Id,
            PasajeroId = usuario.Id,
            RecogidaTexto = "Punto B",
            Estado = EstadoSolicitudViaje.Aceptada
        };

        db.Usuarios.Add(usuario);
        db.Viajes.AddRange(viajePropio, viajeAjeno);
        db.SolicitudesViaje.AddRange(solicitudViajePropio, solicitudComoPasajero);
        var cambioPerfilPendiente = new SolicitudRegistro
        {
            UniversidadId = uni.Id,
            CampusId = campus.Id,
            Nombre = "Propuesta sensible",
            Correo = usuario.Correo,
            HashContrasena = MarcadoresSolicitud.CambioPerfil,
            Rol = RolUsuario.Pasajero,
            Genero = GeneroUsuario.Femenino,
            ImagenPerfil = ImagenPrueba,
            Carrera = "Carrera sensible",
            NumeroIdentificacion = usuario.NumeroIdentificacion,
            Estado = EstadoSolicitudRegistro.Pendiente
        };
        db.SolicitudesRegistro.Add(cambioPerfilPendiente);
        db.Vehiculos.Add(new Vehiculo
        {
            UniversidadId = uni.Id,
            UsuarioId = usuario.Id,
            MarcaModelo = "Kia Rio",
            Placa = "DEL-0001",
            Color = "Negro",
            AsientosTotales = 4,
            Imagen = ImagenPrueba
        });
        db.ContactosEmergencia.Add(new ContactoEmergencia
        {
            UniversidadId = uni.Id,
            UsuarioId = usuario.Id,
            Nombre = "Contacto sensible",
            Relacion = "familiar",
            Telefono = "+593999000000"
        });
        db.TokensRefresco.Add(new TokenRefresco
        {
            UsuarioId = usuario.Id,
            HashToken = Guid.NewGuid().ToString("N"),
            ExpiraEn = ahora.AddDays(1)
        });
        await db.SaveChangesAsync();

        var cache = new MemoryCache(new MemoryCacheOptions());
        var cacheEstado = new CacheEstadoUsuario(db, cache);
        Assert.True((await cacheEstado.ObtenerAsync(usuario.Id)).Permitido);
        var admin = CrearServicio(
            db,
            OmitirFiltros: false,
            universidadId: uni.Id,
            usuarioId: coordinador.Id,
            cacheEstado: cacheEstado);

        await admin.EliminarUsuarioAsync(usuario.Id);

        await db.Entry(usuario).ReloadAsync();
        Assert.Equal(EstadoUsuario.Eliminado, usuario.Estado);
        Assert.Equal("Deleted user", usuario.Nombre);
        Assert.Equal($"deleted-{usuario.Id:D}@invalid.local", usuario.Correo);
        Assert.Null(usuario.ImagenPerfil);
        Assert.Null(usuario.NumeroIdentificacion);
        Assert.Null(usuario.Carrera);
        Assert.Null(usuario.Genero);
        Assert.False(BCrypt.Net.BCrypt.Verify(contrasena, usuario.HashContrasena));
        Assert.Equal(EstadoSolicitudRegistro.Denegada, cambioPerfilPendiente.Estado);
        Assert.Equal("Deleted user", cambioPerfilPendiente.Nombre);
        Assert.Equal(usuario.Correo, cambioPerfilPendiente.Correo);
        Assert.Null(cambioPerfilPendiente.ImagenPerfil);
        Assert.Null(cambioPerfilPendiente.NumeroIdentificacion);
        Assert.Null(cambioPerfilPendiente.Carrera);
        Assert.Equal(EstadoViaje.Cancelado, viajePropio.Estado);
        Assert.Equal(EstadoSolicitudViaje.CanceladaPorConductor, solicitudViajePropio.Estado);
        Assert.Equal(EstadoSolicitudViaje.CanceladaPorPasajero, solicitudComoPasajero.Estado);
        Assert.Equal(2, viajeAjeno.AsientosDisponibles);
        Assert.Empty(await db.Vehiculos.Where(v => v.UsuarioId == usuario.Id).ToListAsync());
        Assert.Empty(await db.ContactosEmergencia.Where(c => c.UsuarioId == usuario.Id).ToListAsync());
        Assert.Empty(await db.TokensRefresco
            .Where(t => t.UsuarioId == usuario.Id && t.RevocadoEn == null)
            .ToListAsync());
        Assert.Contains(await db.EventosAuditoria.ToListAsync(), e =>
            e.Accion == "user.deleted" && e.UsuarioId == usuario.Id);
        Assert.Equal("deleted", (await cacheEstado.ObtenerAsync(usuario.Id)).EstadoUsuario);

        var listado = await admin.ListarUsuariosAsync(new FiltroUsuariosAdmin());
        Assert.DoesNotContain(listado.Items, u => u.Id == usuario.Id);
        var csv = await admin.ExportarUsuariosCsvAsync(new FiltroUsuariosAdmin());
        Assert.DoesNotContain(usuario.Id.ToString(), csv);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Eliminar_usuario_en_viaje_en_curso_devuelve_409(bool comoConductor)
    {
        await using var db = await CrearDbConSeedAsync();
        var uni = await db.Universidades.SingleAsync(u => u.Slug == "utn");
        var campus = await db.Sedes.FirstAsync(c => c.UniversidadId == uni.Id);
        var coordinador = await db.Usuarios.SingleAsync(u => u.Correo == "coordinador@utn.local");
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver3@utn.local");
        var objetivo = new Usuario
        {
            UniversidadId = uni.Id,
            CampusId = campus.Id,
            Rol = comoConductor ? RolUsuario.Conductor : RolUsuario.Pasajero,
            Estado = EstadoUsuario.Activo,
            Nombre = "Usuario viaje activo",
            Correo = $"active-delete-{comoConductor}@utn.local",
            HashContrasena = BCrypt.Net.BCrypt.HashPassword("Prueba123!"),
            Carrera = "Software"
        };
        var viaje = NuevoViaje(
            comoConductor ? objetivo : conductor,
            campus,
            EstadoViaje.EnCurso,
            DateTimeOffset.UtcNow,
            2);
        db.Usuarios.Add(objetivo);
        db.Viajes.Add(viaje);
        if (!comoConductor)
        {
            db.SolicitudesViaje.Add(new SolicitudViaje
            {
                UniversidadId = uni.Id,
                ViajeId = viaje.Id,
                PasajeroId = objetivo.Id,
                RecogidaTexto = "Punto activo",
                Estado = EstadoSolicitudViaje.Aceptada
            });
        }
        await db.SaveChangesAsync();

        var admin = CrearServicio(
            db,
            OmitirFiltros: false,
            universidadId: uni.Id,
            usuarioId: coordinador.Id);
        var ex = await Assert.ThrowsAsync<ExcepcionRegistroUsuarios>(
            () => admin.EliminarUsuarioAsync(objetivo.Id));

        Assert.Equal(409, ex.CodigoEstado);
        Assert.Equal("active_trip_blocks_user_delete", ex.Codigo);
        Assert.Equal(EstadoUsuario.Activo, objetivo.Estado);
    }

    [Fact]
    public async Task Eliminar_usuario_protege_tenant_roles_privilegiados_y_autoborrado()
    {
        await using var db = await CrearDbConSeedAsync();
        var uni = await db.Universidades.SingleAsync(u => u.Slug == "utn");
        var coordinador = await db.Usuarios.SingleAsync(u => u.Correo == "coordinador@utn.local");
        var otroCoordinador = new Usuario
        {
            UniversidadId = uni.Id,
            Rol = RolUsuario.Coordinador,
            Estado = EstadoUsuario.Activo,
            Nombre = "Otro coordinador",
            Correo = "otro-coord@utn.local",
            HashContrasena = BCrypt.Net.BCrypt.HashPassword("Prueba123!")
        };
        var superAdminTenant = new Usuario
        {
            UniversidadId = uni.Id,
            Rol = RolUsuario.SuperAdministrador,
            Estado = EstadoUsuario.Activo,
            Nombre = "Super tenant",
            Correo = "super-tenant@utn.local",
            HashContrasena = BCrypt.Net.BCrypt.HashPassword("Prueba123!")
        };
        db.Usuarios.AddRange(otroCoordinador, superAdminTenant);
        await db.SaveChangesAsync();
        var usuarioOtroTenant = await db.Usuarios.SingleAsync(u => u.Correo == "driver1@puce.local");
        var admin = CrearServicio(
            db,
            OmitirFiltros: false,
            universidadId: uni.Id,
            usuarioId: coordinador.Id);

        var propio = await Assert.ThrowsAsync<ExcepcionRegistroUsuarios>(
            () => admin.EliminarUsuarioAsync(coordinador.Id));
        var coordinadorEx = await Assert.ThrowsAsync<ExcepcionRegistroUsuarios>(
            () => admin.EliminarUsuarioAsync(otroCoordinador.Id));
        var superEx = await Assert.ThrowsAsync<ExcepcionRegistroUsuarios>(
            () => admin.EliminarUsuarioAsync(superAdminTenant.Id));
        var otroTenant = await Assert.ThrowsAsync<ExcepcionRegistroUsuarios>(
            () => admin.EliminarUsuarioAsync(usuarioOtroTenant.Id));

        Assert.Equal("cannot_delete_self", propio.Codigo);
        Assert.Equal("cannot_delete_role", coordinadorEx.Codigo);
        Assert.Equal("cannot_delete_role", superEx.Codigo);
        Assert.Equal(404, otroTenant.CodigoEstado);
        Assert.Equal("user_not_found", otroTenant.Codigo);
    }

    [Fact]
    public async Task Cambio_perfil_pasajero_crea_pendiente_sin_mutar_y_evitar_duplicado()
    {
        await using var db = await CrearDbConSeedAsync();
        var pasajero = await db.Usuarios.SingleAsync(u => u.Correo == "pax2@utn.local");
        var nombreOriginal = pasajero.Nombre;
        var carreraOriginal = pasajero.Carrera;
        var imagenOriginal = pasajero.ImagenPerfil;
        var servicio = CrearServicio(
            db,
            OmitirFiltros: false,
            universidadId: pasajero.UniversidadId,
            usuarioId: pasajero.Id);
        var propuesta = new SolicitudActualizarPerfil
        {
            Nombre = "Nombre propuesto",
            Carrera = "Mecatrónica",
            Genero = "male"
        };

        var respuesta = await servicio.SolicitarCambioPerfilAsync(pasajero.Id, propuesta);

        Assert.Equal("pending", respuesta.Estado);
        Assert.Equal("profile_change", respuesta.Tipo);
        var solicitud = await db.SolicitudesRegistro.SingleAsync(s => s.Id == respuesta.Id);
        Assert.Equal(MarcadoresSolicitud.CambioPerfil, solicitud.HashContrasena);
        Assert.Equal(pasajero.Correo, solicitud.Correo);
        Assert.Equal(pasajero.UniversidadId, solicitud.UniversidadId);
        Assert.Equal(pasajero.CampusId, solicitud.CampusId);
        Assert.Equal(pasajero.NumeroIdentificacion, solicitud.NumeroIdentificacion);
        Assert.Equal(imagenOriginal, solicitud.ImagenPerfil);
        Assert.Equal(nombreOriginal, pasajero.Nombre);
        Assert.Equal(carreraOriginal, pasajero.Carrera);

        var directo = await Assert.ThrowsAsync<ExcepcionRegistroUsuarios>(
            () => servicio.ActualizarPerfilAsync(pasajero.Id, propuesta));
        Assert.Equal(409, directo.CodigoEstado);
        Assert.Equal("profile_change_requires_approval", directo.Codigo);

        var duplicado = await Assert.ThrowsAsync<ExcepcionRegistroUsuarios>(
            () => servicio.SolicitarCambioPerfilAsync(pasajero.Id, propuesta));
        Assert.Equal(409, duplicado.CodigoEstado);
        Assert.Equal("profile_change_pending", duplicado.Codigo);
    }

    [Fact]
    public async Task Aceptar_cambio_perfil_aplica_propuesta_y_revoca_tokens()
    {
        await using var db = await CrearDbConSeedAsync();
        var pasajero = await db.Usuarios.SingleAsync(u => u.Correo == "pax2@utn.local");
        var coordinador = await db.Usuarios.SingleAsync(u => u.Correo == "coordinador@utn.local");
        var auth = CrearAuth(db);
        await auth.IniciarSesionAsync(new SolicitudInicioSesion
        {
            Correo = pasajero.Correo,
            Contrasena = SembradorBaseDatos.ContrasenaPorDefecto
        });
        var servicio = CrearServicio(
            db,
            OmitirFiltros: false,
            universidadId: pasajero.UniversidadId,
            usuarioId: pasajero.Id);
        var respuesta = await servicio.SolicitarCambioPerfilAsync(
            pasajero.Id,
            new SolicitudActualizarPerfil
            {
                Nombre = "Perfil aprobado",
                Carrera = "Telecomunicaciones",
                Genero = "male",
                ImagenPerfil = ImagenPrueba
            });

        var admin = CrearServicio(
            db,
            OmitirFiltros: false,
            universidadId: pasajero.UniversidadId,
            usuarioId: coordinador.Id);
        await admin.AceptarSolicitudAsync(respuesta.Id);
        await db.Entry(pasajero).ReloadAsync();

        Assert.Equal("Perfil aprobado", pasajero.Nombre);
        Assert.Equal("Telecomunicaciones", pasajero.Carrera);
        Assert.Equal(GeneroUsuario.Masculino, pasajero.Genero);
        Assert.Equal(ImagenPrueba, pasajero.ImagenPerfil);
        Assert.Empty(await db.TokensRefresco
            .Where(t => t.UsuarioId == pasajero.Id && t.RevocadoEn == null)
            .ToListAsync());
        Assert.Equal(
            EstadoSolicitudRegistro.Aceptada,
            (await db.SolicitudesRegistro.SingleAsync(s => s.Id == respuesta.Id)).Estado);
    }

    [Fact]
    public async Task Denegar_cambio_perfil_no_modifica_usuario()
    {
        await using var db = await CrearDbConSeedAsync();
        var pasajero = await db.Usuarios.SingleAsync(u => u.Correo == "pax2@utn.local");
        var coordinador = await db.Usuarios.SingleAsync(u => u.Correo == "coordinador@utn.local");
        var nombreOriginal = pasajero.Nombre;
        var carreraOriginal = pasajero.Carrera;
        var generoOriginal = pasajero.Genero;
        var servicio = CrearServicio(
            db,
            OmitirFiltros: false,
            universidadId: pasajero.UniversidadId,
            usuarioId: pasajero.Id);
        var respuesta = await servicio.SolicitarCambioPerfilAsync(
            pasajero.Id,
            new SolicitudActualizarPerfil
            {
                Nombre = "Perfil denegado",
                Carrera = "Otra carrera",
                Genero = "male"
            });

        var admin = CrearServicio(
            db,
            OmitirFiltros: false,
            universidadId: pasajero.UniversidadId,
            usuarioId: coordinador.Id);
        await admin.DenegarSolicitudAsync(respuesta.Id);
        await db.Entry(pasajero).ReloadAsync();

        Assert.Equal(nombreOriginal, pasajero.Nombre);
        Assert.Equal(carreraOriginal, pasajero.Carrera);
        Assert.Equal(generoOriginal, pasajero.Genero);
        Assert.Equal(
            EstadoSolicitudRegistro.Denegada,
            (await db.SolicitudesRegistro.SingleAsync(s => s.Id == respuesta.Id)).Estado);
    }

    private static Viaje NuevoViaje(
        Usuario conductor,
        Campus campus,
        EstadoViaje estado,
        DateTimeOffset saleEn,
        int asientosDisponibles) => new()
    {
        UniversidadId = campus.UniversidadId,
        ConductorId = conductor.Id,
        CampusDestinoId = campus.Id,
        OrigenTexto = "Origen de prueba",
        OrigenLat = 0.35,
        OrigenLng = -78.12,
        SaleEn = saleEn,
        AsientosDisponibles = asientosDisponibles,
        DistanciaKm = 5,
        Estado = estado,
        IniciadoEn = estado == EstadoViaje.EnCurso ? saleEn : null
    };

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
        return new ServicioRegistroUsuarios(
            db,
            inquilino,
            auditoria,
            cacheEstado,
            new CorreoNulo(),
            NullLogger<ServicioRegistroUsuarios>.Instance);
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

    private sealed class CorreoNulo : IServicioCorreo
    {
        public Task EnviarAsync(
            string destinatario,
            string asunto,
            string cuerpo,
            CancellationToken ct = default) => Task.CompletedTask;
    }
}
