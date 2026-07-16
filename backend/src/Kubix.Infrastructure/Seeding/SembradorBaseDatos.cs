using System.Globalization;
using Kubix.Domain.Entities;
using Kubix.Domain.Enums;
using Kubix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kubix.Infrastructure.Seeding;

public sealed class SembradorBaseDatos(
    ContextoApp db,
    IConfiguration configuration,
    ILogger<SembradorBaseDatos> logger)
{
    public const string ContrasenaPorDefecto = "ChangeMe123!";

    /// <summary>
    /// Seed de arranque: solo crea el super_admin si aún no existe.
    /// </summary>
    public async Task SembrarAsync(CancellationToken ct = default)
    {
        if (await db.Usuarios.AnyAsync(u => u.Rol == RolUsuario.SuperAdministrador, ct))
        {
            logger.LogInformation("Seed omitido: ya existe super_admin");
            return;
        }

        var contrasena = configuration["SUPER_ADMIN_PASSWORD"]
            ?? configuration["Seed:SuperAdminPassword"]
            ?? ContrasenaPorDefecto;
        var emailSuper = configuration["SUPER_ADMIN_EMAIL"]
            ?? configuration["Seed:SuperAdminEmail"]
            ?? "superadmin@kubix.local";
        var hash = BCrypt.Net.BCrypt.HashPassword(contrasena);

        var superAdmin = NuevoUsuario(
            null,
            null,
            RolUsuario.SuperAdministrador,
            "Super Admin Kubix",
            emailSuper,
            hash);

        db.Usuarios.Add(superAdmin);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seed completado: solo super_admin ({Correo})", emailSuper);
    }

    /// <summary>
    /// Fixture demo para tests de integración. No se ejecuta al arrancar la API.
    /// </summary>
    public async Task SembrarDemoAsync(CancellationToken ct = default)
    {
        await SembrarAsync(ct);

        if (await db.Universidades.AnyAsync(ct))
        {
            logger.LogInformation("Seed demo omitido: ya existen universidades");
            return;
        }

        var contrasena = configuration["SUPER_ADMIN_PASSWORD"]
            ?? configuration["Seed:SuperAdminPassword"]
            ?? ContrasenaPorDefecto;
        var hash = BCrypt.Net.BCrypt.HashPassword(contrasena);

        var ahora = DateTimeOffset.UtcNow;
        var inicioSemana = InicioSemanaIso(ahora);

        var utn = NuevaUniversidad("Universidad Técnica del Norte", "utn");
        var pue = NuevaUniversidad("Pontificia Universidad Católica del Ecuador", "puce");

        var utnIbarra = NuevoCampus(utn.Id, "Campus Ibarra", "Av. 17 de Julio, Ibarra", -0.3515, -78.1235);
        var utnOtavalo = NuevoCampus(utn.Id, "Campus Otavalo", "Otavalo, Imbabura", 0.2343, -78.2617);
        var pueCentro = NuevoCampus(pue.Id, "Campus Centro", "Av. 12 de Octubre, Quito", -0.2095, -78.4912);
        var pueCumbaya = NuevoCampus(pue.Id, "Campus Cumbayá", "Cumbayá, Quito", -0.2001, -78.4289);

        var configuraciones = new[]
        {
            ConfiguracionPorDefecto(utn.Id, "utn.edu.ec"),
            ConfiguracionPorDefecto(pue.Id, "puce.edu.ec")
        };

        var coordUtn = NuevoUsuario(utn.Id, null, RolUsuario.Coordinador, "Ana Coordinadora UTN", "coordinador@utn.local", hash, debeCambiar: true, genero: GeneroUsuario.Femenino);
        var coordPuce = NuevoUsuario(pue.Id, null, RolUsuario.Coordinador, "Luis Coordinador PUCE", "coordinador@puce.local", hash, debeCambiar: true, genero: GeneroUsuario.Masculino);
        var superAdmin = await db.Usuarios.SingleAsync(u => u.Rol == RolUsuario.SuperAdministrador, ct);

        var conductoresUtn = new[]
        {
            NuevoUsuario(utn.Id, utnIbarra.Id, RolUsuario.Conductor, "Carlos Conductor", "driver1@utn.local", hash, "Software", "1001", calificacion: 4.6m, eco: 24, vitalicio: 24, genero: GeneroUsuario.Masculino),
            NuevoUsuario(utn.Id, utnIbarra.Id, RolUsuario.Conductor, "María Conductora", "driver2@utn.local", hash, "Industrial", "1002", calificacion: 4.2m, eco: 16, vitalicio: 16, genero: GeneroUsuario.Femenino),
            NuevoUsuario(utn.Id, utnOtavalo.Id, RolUsuario.Conductor, "Pedro Conductor", "driver3@utn.local", hash, "Civil", "1003", calificacion: 3.9m, eco: 8, vitalicio: 8, genero: GeneroUsuario.Masculino),
        };

        var pasajerosUtn = new[]
        {
            NuevoUsuario(utn.Id, utnIbarra.Id, RolUsuario.Pasajero, "Sofía Pasajera", "pax1@utn.local", hash, "Software", "2001", calificacion: 4.8m, eco: 14, vitalicio: 14, genero: GeneroUsuario.Femenino),
            NuevoUsuario(utn.Id, utnIbarra.Id, RolUsuario.Pasajero, "Diego Pasajero", "pax2@utn.local", hash, "Software", "2002", calificacion: 4.1m, eco: 8, vitalicio: 8, genero: GeneroUsuario.Masculino),
            NuevoUsuario(utn.Id, utnIbarra.Id, RolUsuario.Pasajero, "Elena Pasajera", "pax3@utn.local", hash, "Industrial", "2003", calificacion: 4.5m, eco: 4, vitalicio: 4, genero: GeneroUsuario.Femenino),
            NuevoUsuario(utn.Id, utnOtavalo.Id, RolUsuario.Pasajero, "Andrés Pasajero", "pax4@utn.local", hash, "Civil", "2004", genero: GeneroUsuario.Masculino),
            NuevoUsuario(utn.Id, utnIbarra.Id, RolUsuario.Pasajero, "Lucía Pendiente", "pending@utn.local", hash, "Software", "2005", estado: EstadoUsuario.Pendiente, genero: GeneroUsuario.Femenino),
        };

        var conductoresPuce = new[]
        {
            NuevoUsuario(pue.Id, pueCentro.Id, RolUsuario.Conductor, "Jorge Conductor", "driver1@puce.local", hash, "Derecho", "3001", calificacion: 4.4m, eco: 8, vitalicio: 8, genero: GeneroUsuario.Masculino),
            NuevoUsuario(pue.Id, pueCumbaya.Id, RolUsuario.Conductor, "Patricia Conductora", "driver2@puce.local", hash, "Medicina", "3002", genero: GeneroUsuario.Femenino),
        };

        var pasajerosPuce = new[]
        {
            NuevoUsuario(pue.Id, pueCentro.Id, RolUsuario.Pasajero, "Camila Pasajera", "pax1@puce.local", hash, "Derecho", "4001", eco: 4, vitalicio: 4, genero: GeneroUsuario.Femenino),
            NuevoUsuario(pue.Id, pueCumbaya.Id, RolUsuario.Pasajero, "Mateo Pasajero", "pax2@puce.local", hash, "Medicina", "4002", genero: GeneroUsuario.Masculino),
            NuevoUsuario(pue.Id, pueCentro.Id, RolUsuario.Pasajero, "Valentina Pasajera", "pax3@puce.local", hash, "Economía", "4003", genero: GeneroUsuario.Femenino),
        };

        var vehiculos = new[]
        {
            NuevoVehiculo(utn.Id, conductoresUtn[0].Id, "Chevrolet Sail", "PBA-1001", "Blanco", 3),
            NuevoVehiculo(utn.Id, conductoresUtn[1].Id, "Kia Rio", "PBA-1002", "Gris", 3),
            NuevoVehiculo(utn.Id, conductoresUtn[2].Id, "Hyundai Accent", "PBA-1003", "Negro", 2),
            NuevoVehiculo(pue.Id, conductoresPuce[0].Id, "Toyota Yaris", "PBA-2001", "Rojo", 3),
            NuevoVehiculo(pue.Id, conductoresPuce[1].Id, "Nissan Versa", "PBA-2002", "Azul", 3),
        };

        var viajeCompletado1 = NuevoViaje(utn.Id, conductoresUtn[0].Id, utnIbarra.Id, "Centro Ibarra", -0.34, -78.12,
            inicioSemana.AddDays(1).AddHours(7), 0, EstadoViaje.Completado, 8.5m, 1.785m, inicioSemana.AddDays(1).AddHours(7), inicioSemana.AddDays(1).AddHours(7).AddMinutes(35));
        var viajeCompletado2 = NuevoViaje(utn.Id, conductoresUtn[0].Id, utnIbarra.Id, "La Dolorosa", -0.345, -78.13,
            inicioSemana.AddDays(2).AddHours(7), 1, EstadoViaje.Completado, 6.2m, 1.302m, inicioSemana.AddDays(2).AddHours(7), inicioSemana.AddDays(2).AddHours(7).AddMinutes(28));
        var viajeCompletado3 = NuevoViaje(utn.Id, conductoresUtn[1].Id, utnIbarra.Id, "El Ejido", -0.348, -78.118,
            inicioSemana.AddDays(3).AddHours(7), 0, EstadoViaje.Completado, 7.1m, 1.491m, inicioSemana.AddDays(3).AddHours(7), inicioSemana.AddDays(3).AddHours(7).AddMinutes(30));
        var viajeProgramado = NuevoViaje(utn.Id, conductoresUtn[0].Id, utnIbarra.Id, "Terminal Terrestre", -0.355, -78.125,
            ahora.AddHours(6), 2, EstadoViaje.Programado, 5.0m);
        var viajeEnCurso = NuevoViaje(utn.Id, conductoresUtn[1].Id, utnIbarra.Id, "San Antonio", -0.36, -78.14,
            ahora.AddMinutes(-20), 1, EstadoViaje.EnCurso, 9.0m, iniciadoEn: ahora.AddMinutes(-15));
        var viajePuceCompletado = NuevoViaje(pue.Id, conductoresPuce[0].Id, pueCentro.Id, "La Carolina", -0.18, -78.48,
            inicioSemana.AddDays(1).AddHours(8), 1, EstadoViaje.Completado, 4.5m, 0.945m, inicioSemana.AddDays(1).AddHours(8), inicioSemana.AddDays(1).AddHours(8).AddMinutes(25));

        var solicitudesViaje = new[]
        {
            NuevaSolicitudViaje(utn.Id, viajeCompletado1.Id, pasajerosUtn[0].Id, "Parque Ciudad", -0.342, -78.121, EstadoSolicitudViaje.Aceptada),
            NuevaSolicitudViaje(utn.Id, viajeCompletado1.Id, pasajerosUtn[1].Id, "Mercado Central", -0.343, -78.119, EstadoSolicitudViaje.Aceptada),
            NuevaSolicitudViaje(utn.Id, viajeCompletado2.Id, pasajerosUtn[0].Id, "Parque Ciudad", -0.342, -78.121, EstadoSolicitudViaje.Aceptada),
            NuevaSolicitudViaje(utn.Id, viajeCompletado3.Id, pasajerosUtn[2].Id, "Av. Teodoro Gómez", -0.35, -78.12, EstadoSolicitudViaje.Aceptada),
            NuevaSolicitudViaje(utn.Id, viajeProgramado.Id, pasajerosUtn[1].Id, "Gasolinera Primax", -0.352, -78.124, EstadoSolicitudViaje.Pendiente),
            NuevaSolicitudViaje(utn.Id, viajeEnCurso.Id, pasajerosUtn[0].Id, "San Antonio norte", -0.358, -78.138, EstadoSolicitudViaje.Aceptada),
            NuevaSolicitudViaje(pue.Id, viajePuceCompletado.Id, pasajerosPuce[0].Id, "Amazonas", -0.19, -78.49, EstadoSolicitudViaje.Aceptada),
        };

        var eco = new List<TransaccionEcoToken>
        {
            Eco(utn.Id, conductoresUtn[0].Id, TipoTransaccionEcoToken.ViajeCompletadoConductor, 8, viajeCompletado1.Id.ToString(), inicioSemana.AddDays(1).AddHours(8)),
            Eco(utn.Id, pasajerosUtn[0].Id, TipoTransaccionEcoToken.ViajeCompletadoPasajero, 4, viajeCompletado1.Id.ToString(), inicioSemana.AddDays(1).AddHours(8)),
            Eco(utn.Id, pasajerosUtn[1].Id, TipoTransaccionEcoToken.ViajeCompletadoPasajero, 4, viajeCompletado1.Id.ToString(), inicioSemana.AddDays(1).AddHours(8)),
            Eco(utn.Id, conductoresUtn[0].Id, TipoTransaccionEcoToken.ViajeCompletadoConductor, 8, viajeCompletado2.Id.ToString(), inicioSemana.AddDays(2).AddHours(8)),
            Eco(utn.Id, pasajerosUtn[0].Id, TipoTransaccionEcoToken.ViajeCompletadoPasajero, 4, viajeCompletado2.Id.ToString(), inicioSemana.AddDays(2).AddHours(8)),
            Eco(utn.Id, conductoresUtn[1].Id, TipoTransaccionEcoToken.ViajeCompletadoConductor, 8, viajeCompletado3.Id.ToString(), inicioSemana.AddDays(3).AddHours(8)),
            Eco(utn.Id, pasajerosUtn[2].Id, TipoTransaccionEcoToken.ViajeCompletadoPasajero, 4, viajeCompletado3.Id.ToString(), inicioSemana.AddDays(3).AddHours(8)),
            Eco(utn.Id, pasajerosUtn[0].Id, TipoTransaccionEcoToken.CalificacionEnviada, 2, "rating-seed-1", inicioSemana.AddDays(1).AddHours(9)),
            Eco(utn.Id, conductoresUtn[0].Id, TipoTransaccionEcoToken.RachaSemanal, 10, ClaveSemanaIso(inicioSemana), inicioSemana.AddDays(3).AddHours(9)),
            Eco(pue.Id, conductoresPuce[0].Id, TipoTransaccionEcoToken.ViajeCompletadoConductor, 8, viajePuceCompletado.Id.ToString(), inicioSemana.AddDays(1).AddHours(9)),
            Eco(pue.Id, pasajerosPuce[0].Id, TipoTransaccionEcoToken.ViajeCompletadoPasajero, 4, viajePuceCompletado.Id.ToString(), inicioSemana.AddDays(1).AddHours(9)),
        };

        conductoresUtn[0].BalanceEco = 8 + 8 + 10;
        conductoresUtn[0].EcoVitalicio = conductoresUtn[0].BalanceEco;
        conductoresUtn[1].BalanceEco = 8;
        conductoresUtn[1].EcoVitalicio = 8;
        pasajerosUtn[0].BalanceEco = 4 + 4 + 2;
        pasajerosUtn[0].EcoVitalicio = 10;
        pasajerosUtn[1].BalanceEco = 4;
        pasajerosUtn[1].EcoVitalicio = 4;
        pasajerosUtn[2].BalanceEco = 4;
        pasajerosUtn[2].EcoVitalicio = 4;
        conductoresPuce[0].BalanceEco = 8;
        conductoresPuce[0].EcoVitalicio = 8;
        pasajerosPuce[0].BalanceEco = 4;
        pasajerosPuce[0].EcoVitalicio = 4;

        var calificaciones = new[]
        {
            new Calificacion
            {
                UniversidadId = utn.Id,
                ViajeId = viajeCompletado1.Id,
                CalificadorId = pasajerosUtn[0].Id,
                CalificadoId = conductoresUtn[0].Id,
                Estrellas = 5,
                Comentario = "Excelente viaje"
            }
        };

        var sos = new AlertaSos
        {
            UniversidadId = utn.Id,
            ViajeId = viajeEnCurso.Id,
            UsuarioId = pasajerosUtn[0].Id,
            Lat = -0.357,
            Lng = -78.137,
            DisparadaEn = ahora.AddMinutes(-5),
            Estado = EstadoAlertaSos.Activa
        };

        var contactos = new[]
        {
            new ContactoEmergencia
            {
                UniversidadId = utn.Id,
                UsuarioId = pasajerosUtn[0].Id,
                Nombre = "Mamá Sofía",
                Relacion = "madre",
                Telefono = "+593991111111"
            }
        };

        var pings = new[]
        {
            new PingUbicacion
            {
                UniversidadId = utn.Id,
                ViajeId = viajeEnCurso.Id,
                UsuarioId = conductoresUtn[1].Id,
                Lat = -0.359,
                Lng = -78.139,
                RegistradoEn = ahora.AddMinutes(-2)
            },
            new PingUbicacion
            {
                UniversidadId = utn.Id,
                ViajeId = viajeEnCurso.Id,
                UsuarioId = pasajerosUtn[0].Id,
                Lat = -0.358,
                Lng = -78.138,
                RegistradoEn = ahora.AddMinutes(-1)
            }
        };

        var auditorias = new[]
        {
            new EventoAuditoria
            {
                UniversidadId = utn.Id,
                UsuarioId = pasajerosUtn[0].Id,
                Accion = "sos.fired",
                Tipo = TipoEventoAuditoria.Sos,
                Severidad = SeveridadAuditoria.Alta
            },
            new EventoAuditoria
            {
                UniversidadId = null,
                UsuarioId = superAdmin.Id,
                Accion = "platform.seeded",
                Tipo = TipoEventoAuditoria.Sistema,
                Severidad = SeveridadAuditoria.Baja
            }
        };

        var notificaciones = new[]
        {
            new Notificacion
            {
                UniversidadId = utn.Id,
                RolDestinatario = RolUsuario.Coordinador,
                Tipo = TipoNotificacion.Sos,
                Titulo = "Alerta SOS activa",
                Cuerpo = "Sofía Pasajera disparó una alerta SOS"
            }
        };

        var registroPendiente = new SolicitudRegistro
        {
            UniversidadId = utn.Id,
            CampusId = utnIbarra.Id,
            Nombre = "Nuevo Conductor",
            Correo = "nuevo.driver@utn.edu.ec",
            HashContrasena = hash,
            Rol = RolUsuario.Conductor,
            Genero = GeneroUsuario.Masculino,
            Carrera = "Software",
            NumeroIdentificacion = "9999",
            VehiculoJson = """{"makeModel":"Chevrolet Spark","plate":"PBA-9999","color":"Verde","seatsTotal":3}""",
            Estado = EstadoSolicitudRegistro.Pendiente
        };

        db.Universidades.AddRange(utn, pue);
        db.Sedes.AddRange(utnIbarra, utnOtavalo, pueCentro, pueCumbaya);
        db.ConfiguracionesUniversidad.AddRange(configuraciones);
        db.Usuarios.AddRange(coordUtn, coordPuce);
        db.Usuarios.AddRange(conductoresUtn);
        db.Usuarios.AddRange(pasajerosUtn);
        db.Usuarios.AddRange(conductoresPuce);
        db.Usuarios.AddRange(pasajerosPuce);
        db.Vehiculos.AddRange(vehiculos);
        db.Viajes.AddRange(viajeCompletado1, viajeCompletado2, viajeCompletado3, viajeProgramado, viajeEnCurso, viajePuceCompletado);

        var puntosRuta = new[]
        {
            NuevoPuntoRuta(viajeCompletado1, utnIbarra, 0),
            NuevoPuntoRuta(viajeCompletado1, utnIbarra, 1),
            NuevoPuntoRuta(viajeCompletado2, utnIbarra, 0),
            NuevoPuntoRuta(viajeCompletado2, utnIbarra, 1),
            NuevoPuntoRuta(viajeCompletado3, utnIbarra, 0),
            NuevoPuntoRuta(viajeCompletado3, utnIbarra, 1),
            NuevoPuntoRuta(viajeProgramado, utnIbarra, 0),
            NuevoPuntoRuta(viajeProgramado, utnIbarra, 1),
            NuevoPuntoRuta(viajeEnCurso, utnIbarra, 0),
            NuevoPuntoRuta(viajeEnCurso, utnIbarra, 1),
            NuevoPuntoRuta(viajePuceCompletado, pueCentro, 0),
            NuevoPuntoRuta(viajePuceCompletado, pueCentro, 1),
        };
        db.PuntosRutaViaje.AddRange(puntosRuta);

        db.SolicitudesViaje.AddRange(solicitudesViaje);
        db.TransaccionesEcoToken.AddRange(eco);
        db.Calificaciones.AddRange(calificaciones);
        db.AlertasSos.Add(sos);
        db.ContactosEmergencia.AddRange(contactos);
        db.PingsUbicacion.AddRange(pings);
        db.EventosAuditoria.AddRange(auditorias);
        db.Notificaciones.AddRange(notificaciones);
        db.SolicitudesRegistro.Add(registroPendiente);

        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Seed demo completado: universidades=2, usuarios={Usuarios}",
            await db.Usuarios.CountAsync(ct));
    }

    private static Universidad NuevaUniversidad(string nombre, string slug) => new()
    {
        Nombre = nombre,
        Slug = slug,
        Estado = EstadoUniversidad.Activa
    };

    private static Campus NuevoCampus(Guid universidadId, string nombre, string direccion, double lat, double lng) => new()
    {
        UniversidadId = universidadId,
        Nombre = nombre,
        Direccion = direccion,
        Lat = lat,
        Lng = lng
    };

    private static ConfiguracionUniversidad ConfiguracionPorDefecto(Guid universidadId, string dominio) => new()
    {
        UniversidadId = universidadId,
        DominioCorreoPermitido = dominio,
        ZonaHoraria = "America/Guayaquil",
        CorreoSoporte = $"soporte@{dominio}",
        MaxViajesDiariosPorConductor = 6,
        CalificacionMinimaConductor = 3.5m,
        FactorCo2KgKm = 0.21m,
        GamificacionHabilitada = true,
        SeguimientoCo2Habilitado = true,
        NotificarSos = true,
        NotificarBloqueo = true,
        NotificarReporteSemanal = true
    };

    private static Usuario NuevoUsuario(
        Guid? universidadId,
        Guid? campusId,
        RolUsuario rol,
        string nombre,
        string email,
        string hash,
        string? carrera = null,
        string? numeroIdentificacion = null,
        decimal calificacion = 0,
        int eco = 0,
        int vitalicio = 0,
        bool debeCambiar = false,
        EstadoUsuario estado = EstadoUsuario.Activo,
        GeneroUsuario? genero = null) => new()
    {
        UniversidadId = universidadId,
        CampusId = campusId,
        Rol = rol,
        Estado = estado,
        Nombre = nombre,
        Correo = email,
        HashContrasena = hash,
        Genero = genero,
        Carrera = carrera,
        NumeroIdentificacion = numeroIdentificacion,
        PromedioCalificacion = calificacion,
        BalanceEco = eco,
        EcoVitalicio = vitalicio,
        DebeCambiarContrasena = debeCambiar
    };

    private static Vehiculo NuevoVehiculo(Guid universidadId, Guid usuarioId, string marca, string placa, string color, int asientos) => new()
    {
        UniversidadId = universidadId,
        UsuarioId = usuarioId,
        MarcaModelo = marca,
        Placa = placa,
        Color = color,
        AsientosTotales = asientos
    };

    private static Viaje NuevoViaje(
        Guid universidadId,
        Guid conductorId,
        Guid campusId,
        string origen,
        double lat,
        double lng,
        DateTimeOffset salida,
        int asientos,
        EstadoViaje estado,
        decimal distancia,
        decimal co2 = 0,
        DateTimeOffset? iniciadoEn = null,
        DateTimeOffset? completadoEn = null) => new()
    {
        UniversidadId = universidadId,
        ConductorId = conductorId,
        CampusDestinoId = campusId,
        OrigenTexto = origen,
        OrigenLat = lat,
        OrigenLng = lng,
        SaleEn = salida,
        AsientosDisponibles = asientos,
        DistanciaKm = distancia,
        Estado = estado,
        IniciadoEn = iniciadoEn,
        CompletadoEn = completadoEn,
        Co2AhorradoKg = co2,
        Polilinea = null
    };

    private static PuntoRutaViaje NuevoPuntoRuta(Viaje viaje, Campus campus, int seq)
    {
        if (seq == 0)
        {
            return new PuntoRutaViaje
            {
                UniversidadId = viaje.UniversidadId,
                ViajeId = viaje.Id,
                Seq = 0,
                Lat = viaje.OrigenLat,
                Lng = viaje.OrigenLng,
                Etiqueta = viaje.OrigenTexto
            };
        }

        return new PuntoRutaViaje
        {
            UniversidadId = viaje.UniversidadId,
            ViajeId = viaje.Id,
            Seq = seq,
            Lat = (viaje.OrigenLat + campus.Lat) / 2.0,
            Lng = (viaje.OrigenLng + campus.Lng) / 2.0,
            Etiqueta = null
        };
    }

    private static SolicitudViaje NuevaSolicitudViaje(
        Guid universidadId,
        Guid viajeId,
        Guid pasajeroId,
        string recogida,
        double lat,
        double lng,
        EstadoSolicitudViaje estado) => new()
    {
        UniversidadId = universidadId,
        ViajeId = viajeId,
        PasajeroId = pasajeroId,
        RecogidaTexto = recogida,
        RecogidaLat = lat,
        RecogidaLng = lng,
        Estado = estado
    };

    private static TransaccionEcoToken Eco(
        Guid universidadId,
        Guid usuarioId,
        TipoTransaccionEcoToken tipo,
        int monto,
        string idFuente,
        DateTimeOffset creadoEn) => new()
    {
        UniversidadId = universidadId,
        UsuarioId = usuarioId,
        Tipo = tipo,
        Monto = monto,
        IdFuente = idFuente,
        CreadoEn = creadoEn
    };

    private static DateTimeOffset InicioSemanaIso(DateTimeOffset ahora)
    {
        var fecha = ahora.UtcDateTime.Date;
        var diff = ((int)fecha.DayOfWeek + 6) % 7; // lunes = 0
        return new DateTimeOffset(fecha.AddDays(-diff), TimeSpan.Zero);
    }

    private static string ClaveSemanaIso(DateTimeOffset inicioSemana)
    {
        var cal = CultureInfo.InvariantCulture.Calendar;
        var semana = cal.GetWeekOfYear(inicioSemana.UtcDateTime, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        return $"{inicioSemana.Year}-W{semana:D2}";
    }
}
