using System.Text;
using Kubix.Application.Admin;
using Kubix.Domain.Entities;
using Kubix.Domain.Enums;
using Kubix.Infrastructure.Admin;
using Kubix.Infrastructure.Persistence;
using Kubix.Infrastructure.Seeding;
using Kubix.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kubix.Tests;

public class PruebasAdminOps
{
    [Fact]
    public async Task Dashboard_numeros_coinciden_con_seed()
    {
        await using var db = await CrearDbConSeedAsync();
        var coord = await db.Usuarios.SingleAsync(u => u.Correo == "coordinador@utn.local");
        var servicio = CrearServicio(db, coord);

        var dash = await servicio.ObtenerDashboardAsync();

        // Al menos el viaje en curso del seed cae en el día local de la universidad.
        Assert.True(dash.ViajesHoy >= 1, $"tripsToday esperado ≥1, fue {dash.ViajesHoy}");
        Assert.Equal(0, dash.UsuariosBloqueados);

        var co2Esperado = await db.Viajes
            .Where(v => v.UniversidadId == coord.UniversidadId && v.Estado == EstadoViaje.Completado)
            .SumAsync(v => v.Co2AhorradoKg);
        Assert.Equal(decimal.Round(co2Esperado, 3), dash.Co2Ahorrado);

        // 7 activos de 8 driver+passenger UTN (pending@utn.local está Pendiente) → 87.5%
        Assert.Equal(87.5m, dash.TasaAdopcion);
        Assert.Equal("active_users_over_total", dash.BaseTasaAdopcion);

        Assert.NotEmpty(dash.SosActivos);
        Assert.All(dash.SosActivos, s => Assert.Equal("active", s.Estado));
        Assert.True(dash.GamificacionHabilitada);
        Assert.Null(dash.NotaXpPorCarrera);
    }

    [Fact]
    public async Task XpPorCarrera_solo_montos_positivos()
    {
        await using var db = await CrearDbConSeedAsync();
        var coord = await db.Usuarios.SingleAsync(u => u.Correo == "coordinador@utn.local");
        var driver = await db.Usuarios.SingleAsync(u => u.Correo == "driver1@utn.local");
        var pax = await db.Usuarios.SingleAsync(u => u.Correo == "pax1@utn.local");

        // Transacciones de la semana actual (UtcNow) para no depender del borde lun–dom del seed.
        db.TransaccionesEcoToken.AddRange(
            new TransaccionEcoToken
            {
                UniversidadId = coord.UniversidadId!.Value,
                UsuarioId = driver.Id,
                Tipo = TipoTransaccionEcoToken.ViajeCompletadoConductor,
                Monto = 8,
                IdFuente = "xp-test-pos",
                CreadoEn = DateTimeOffset.UtcNow
            },
            new TransaccionEcoToken
            {
                UniversidadId = coord.UniversidadId!.Value,
                UsuarioId = pax.Id,
                Tipo = TipoTransaccionEcoToken.ViajeCompletadoPasajero,
                Monto = 4,
                IdFuente = "xp-test-pos",
                CreadoEn = DateTimeOffset.UtcNow
            },
            new TransaccionEcoToken
            {
                UniversidadId = coord.UniversidadId!.Value,
                UsuarioId = driver.Id,
                Tipo = TipoTransaccionEcoToken.PenalizacionCancelacionTardia,
                Monto = -5,
                IdFuente = "penalty-test",
                CreadoEn = DateTimeOffset.UtcNow
            });
        await db.SaveChangesAsync();

        var dash = await CrearServicio(db, coord).ObtenerDashboardAsync();
        Assert.NotEmpty(dash.XpPorCarrera);
        Assert.All(dash.XpPorCarrera, x => Assert.True(x.Xp > 0));

        var software = dash.XpPorCarrera.Single(x => x.Carrera == "Software");
        // Solo las filas positivas de esta prueba (+ las del seed si caen en la semana).
        // Penalización −5 no debe restar; mínimo 8+4=12 de las txs insertadas.
        Assert.True(software.Xp >= 12, $"Software XP={software.Xp}");
    }

    [Fact]
    public async Task XpPorCarrera_vacio_si_gamificacion_off()
    {
        await using var db = await CrearDbConSeedAsync();
        var coord = await db.Usuarios.SingleAsync(u => u.Correo == "coordinador@utn.local");
        var config = await db.ConfiguracionesUniversidad.SingleAsync(c =>
            c.UniversidadId == coord.UniversidadId);
        config.GamificacionHabilitada = false;
        await db.SaveChangesAsync();

        var dash = await CrearServicio(db, coord).ObtenerDashboardAsync();
        Assert.False(dash.GamificacionHabilitada);
        Assert.Empty(dash.XpPorCarrera);
        Assert.Contains("disabled", dash.NotaXpPorCarrera!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Settings_persisten_en_put()
    {
        await using var db = await CrearDbConSeedAsync();
        var coord = await db.Usuarios.SingleAsync(u => u.Correo == "coordinador@utn.local");
        var servicio = CrearServicio(db, coord);

        var actualizado = await servicio.ActualizarConfiguracionAsync(new SolicitudActualizarConfiguracion
        {
            ZonaHoraria = "America/Bogota",
            CorreoSoporte = "ayuda@utn.edu.ec",
            DominioCorreoPermitido = "estudiantes.utn.edu.ec",
            MaxViajesDiarios = 4,
            CalificacionMinimaConductor = 3.0m,
            FactorCo2KgKm = 0.18m,
            GamificacionHabilitada = false,
            SeguimientoCo2Habilitado = false,
            NotificarSos = false,
            NotificarBloqueo = true,
            NotificarReporteSemanal = false
        });

        Assert.Equal("America/Bogota", actualizado.ZonaHoraria);
        Assert.Equal("ayuda@utn.edu.ec", actualizado.CorreoSoporte);
        Assert.Equal("estudiantes.utn.edu.ec", actualizado.DominioCorreoPermitido);
        Assert.Equal(4, actualizado.MaxViajesDiarios);
        Assert.Equal(3.0m, actualizado.CalificacionMinimaConductor);
        Assert.Equal(0.18m, actualizado.FactorCo2KgKm);
        Assert.False(actualizado.GamificacionHabilitada);
        Assert.False(actualizado.SeguimientoCo2Habilitado);
        Assert.False(actualizado.NotificarSos);
        Assert.True(actualizado.NotificarBloqueo);
        Assert.False(actualizado.NotificarReporteSemanal);

        var leido = await servicio.ObtenerConfiguracionAsync();
        Assert.Equal("America/Bogota", leido.ZonaHoraria);
        Assert.Equal(4, leido.MaxViajesDiarios);
        Assert.False(leido.GamificacionHabilitada);
    }

    [Fact]
    public async Task Marcar_notificacion_leida_y_read_all()
    {
        await using var db = await CrearDbConSeedAsync();
        var coord = await db.Usuarios.SingleAsync(u => u.Correo == "coordinador@utn.local");
        var servicio = CrearServicio(db, coord);

        var lista = await servicio.ListarNotificacionesAsync(coord.Id);
        Assert.NotEmpty(lista);
        Assert.All(lista, n => Assert.False(n.Leida));

        var primera = lista[0];
        await servicio.MarcarNotificacionLeidaAsync(coord.Id, primera.Id);

        var trasUna = await servicio.ListarNotificacionesAsync(coord.Id);
        Assert.True(trasUna.Single(n => n.Id == primera.Id).Leida);

        db.Notificaciones.Add(new Notificacion
        {
            UniversidadId = coord.UniversidadId!.Value,
            RolDestinatario = RolUsuario.Coordinador,
            Tipo = TipoNotificacion.Sistema,
            Titulo = "Segunda",
            Cuerpo = "Otra alerta",
            Leida = false
        });
        await db.SaveChangesAsync();

        await servicio.MarcarTodasNotificacionesLeidasAsync(coord.Id);
        var todas = await servicio.ListarNotificacionesAsync(coord.Id);
        Assert.All(todas, n => Assert.True(n.Leida));
    }

    [Fact]
    public async Task Reportes_period_incluye_viajes_del_rango()
    {
        await using var db = await CrearDbConSeedAsync();
        var coord = await db.Usuarios.SingleAsync(u => u.Correo == "coordinador@utn.local");
        var driver = await db.Usuarios.SingleAsync(u => u.Correo == "driver1@utn.local");
        var campus = await db.Sedes.FirstAsync(c => c.UniversidadId == coord.UniversidadId);

        // Viaje anclado a UtcNow para diario/semanal/anual.
        db.Viajes.Add(new Viaje
        {
            UniversidadId = coord.UniversidadId!.Value,
            ConductorId = driver.Id,
            CampusDestinoId = campus.Id,
            OrigenTexto = "Test Reportes",
            OrigenLat = -0.35,
            OrigenLng = -78.12,
            SaleEn = DateTimeOffset.UtcNow,
            AsientosDisponibles = 2,
            DistanciaKm = 3.5m,
            Co2AhorradoKg = 0.735m,
            Estado = EstadoViaje.Completado,
            CompletadoEn = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var servicio = CrearServicio(db, coord);

        var diario = await servicio.ObtenerReporteAsync("diario");
        Assert.Equal("diario", diario.Periodo);
        Assert.Contains(diario.Viajes, v => v.OrigenTexto == "Test Reportes");
        Assert.NotEmpty(diario.ChartSemanal);

        var semanal = await servicio.ObtenerReporteAsync("semanal");
        Assert.Equal("semanal", semanal.Periodo);
        Assert.True(semanal.Kpis.Viajes >= 1);
        Assert.Contains(semanal.Viajes, v => v.OrigenTexto == "Test Reportes");

        var mensual = await servicio.ObtenerReporteAsync("mensual");
        Assert.Equal("mensual", mensual.Periodo);
        Assert.True(mensual.Kpis.Viajes >= 1);

        var trimestral = await servicio.ObtenerReporteAsync("trimestral");
        Assert.Equal("trimestral", trimestral.Periodo);

        var anual = await servicio.ObtenerReporteAsync("anual");
        Assert.Equal("anual", anual.Periodo);
        Assert.True(anual.Kpis.Viajes >= 1);
    }

    [Fact]
    public async Task Export_csv_devuelve_contenido()
    {
        await using var db = await CrearDbConSeedAsync();
        var coord = await db.Usuarios.SingleAsync(u => u.Correo == "coordinador@utn.local");
        var driver = await db.Usuarios.SingleAsync(u => u.Correo == "driver1@utn.local");
        var campus = await db.Sedes.FirstAsync(c => c.UniversidadId == coord.UniversidadId);

        db.Viajes.Add(new Viaje
        {
            UniversidadId = coord.UniversidadId!.Value,
            ConductorId = driver.Id,
            CampusDestinoId = campus.Id,
            OrigenTexto = "Export Origin",
            OrigenLat = -0.35,
            OrigenLng = -78.12,
            SaleEn = DateTimeOffset.UtcNow,
            AsientosDisponibles = 1,
            DistanciaKm = 2m,
            Estado = EstadoViaje.Programado
        });
        await db.SaveChangesAsync();

        var servicio = CrearServicio(db, coord);

        var archivo = await servicio.ExportarReporteAsync("semanal", "csv");
        Assert.Equal("text/csv; charset=utf-8", archivo.ContentType);
        Assert.EndsWith(".csv", archivo.NombreArchivo);
        Assert.NotEmpty(archivo.Contenido);

        var texto = Encoding.UTF8.GetString(archivo.Contenido);
        Assert.Contains("id,status,originText", texto);
        Assert.Contains("Export Origin", texto);
        Assert.Contains("scheduled", texto);

        var xlsx = await servicio.ExportarReporteAsync("mensual", "xlsx");
        Assert.Contains("spreadsheetml", xlsx.ContentType);
        Assert.True(xlsx.Contenido.Length > 100);

        var pdf = await servicio.ExportarReporteAsync("trimestral", "pdf");
        Assert.Equal("application/pdf", pdf.ContentType);
        Assert.True(pdf.Contenido.Length > 100);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf.Contenido, 0, 4));
    }

    [Fact]
    public async Task Audit_log_filtra_por_tipo()
    {
        await using var db = await CrearDbConSeedAsync();
        var coord = await db.Usuarios.SingleAsync(u => u.Correo == "coordinador@utn.local");
        var servicio = CrearServicio(db, coord);

        var pagina = await servicio.ListarAuditoriaAsync("sos", null, 1, 20);
        Assert.True(pagina.Total >= 1);
        Assert.All(pagina.Items, i => Assert.Equal("sos", i.Tipo));
    }

    [Fact]
    public async Task Export_auditoria_devuelve_pdf_completo()
    {
        await using var db = await CrearDbConSeedAsync();
        var coord = await db.Usuarios.SingleAsync(u => u.Correo == "coordinador@utn.local");
        var servicio = CrearServicio(db, coord);

        var archivo = await servicio.ExportarAuditoriaPdfAsync("sos", null);

        Assert.Equal("application/pdf", archivo.ContentType);
        Assert.EndsWith(".pdf", archivo.NombreArchivo);
        Assert.True(archivo.Contenido.Length > 100);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(archivo.Contenido, 0, 4));
    }

    private static IServicioAdminOps CrearServicio(ContextoApp db, Usuario usuario)
    {
        if (db is not ContextoAppConInquilino mutable)
        {
            throw new InvalidOperationException("Se espera ContextoAppConInquilino.");
        }

        mutable.Inquilino.OmitirFiltros = false;
        mutable.Inquilino.UniversidadId = usuario.UniversidadId;
        mutable.Inquilino.CampusId = usuario.CampusId;
        mutable.Inquilino.UsuarioId = usuario.Id;
        mutable.Inquilino.Rol = usuario.Rol;
        return new ServicioAdminOps(db, mutable.Inquilino);
    }

    private static async Task<ContextoApp> CrearDbConSeedAsync()
    {
        var inquilino = new ContextoInquilino { OmitirFiltros = true };
        var db = new ContextoAppConInquilino(CrearOpciones(), inquilino);
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

    private static DbContextOptions<ContextoApp> CrearOpciones() =>
        new DbContextOptionsBuilder<ContextoApp>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private sealed class ContextoAppConInquilino(
        DbContextOptions<ContextoApp> options,
        ContextoInquilino inquilino) : ContextoApp(options, inquilino)
    {
        public ContextoInquilino Inquilino { get; } = inquilino;
    }
}
