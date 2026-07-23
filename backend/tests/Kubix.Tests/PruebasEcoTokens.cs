using Kubix.Application.EcoTokens;
using Kubix.Domain.Entities;
using Kubix.Domain.Enums;
using Kubix.Infrastructure.EcoTokens;
using Kubix.Infrastructure.Persistence;
using Kubix.Infrastructure.Seeding;
using Kubix.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kubix.Tests;

public class PruebasEcoTokens
{
    [Fact]
    public async Task Completar_viaje_acredita_8_y_4_una_vez_replay_noop()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver3@utn.local");
        var pax = await db.Usuarios.SingleAsync(u => u.Correo == "pax4@utn.local");
        await ResetearEcoAsync(db, conductor);
        await ResetearEcoAsync(db, pax);

        var viaje = await CrearViajeCompletadoConPaxAsync(db, conductor, pax);
        var motor = new MotorEcoTokens(db);

        await motor.AlCompletarViajeAsync(viaje.Id);
        await motor.AlCompletarViajeAsync(viaje.Id); // replay

        await db.Entry(conductor).ReloadAsync();
        await db.Entry(pax).ReloadAsync();

        Assert.Equal(8, conductor.BalanceEco);
        Assert.Equal(8, conductor.EcoVitalicio);
        Assert.Equal(4, pax.BalanceEco);
        Assert.Equal(4, pax.EcoVitalicio);

        Assert.Equal(1, await db.TransaccionesEcoToken.CountAsync(t =>
            t.UsuarioId == conductor.Id
            && t.Tipo == TipoTransaccionEcoToken.ViajeCompletadoConductor
            && t.IdFuente == viaje.Id.ToString()));
        Assert.Equal(1, await db.TransaccionesEcoToken.CountAsync(t =>
            t.UsuarioId == pax.Id
            && t.Tipo == TipoTransaccionEcoToken.ViajeCompletadoPasajero
            && t.IdFuente == viaje.Id.ToString()));

        Assert.Equal(
            await db.TransaccionesEcoToken.Where(t => t.UsuarioId == conductor.Id).SumAsync(t => t.Monto),
            conductor.BalanceEco);
        Assert.Equal(
            await db.TransaccionesEcoToken.Where(t => t.UsuarioId == pax.Id).SumAsync(t => t.Monto),
            pax.BalanceEco);
    }

    [Fact]
    public async Task Quinto_completado_en_semana_da_racha_10_una_vez()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver3@utn.local");
        await ResetearEcoAsync(db, conductor);

        var config = await db.ConfiguracionesUniversidad.SingleAsync(c =>
            c.UniversidadId == conductor.UniversidadId);
        var (_, _, claveSemana) = MotorEcoTokens.VentanaSemanaUniversidad(
            DateTimeOffset.UtcNow,
            config.ZonaHoraria);

        // 4 trip_completed_driver previos en la semana actual.
        for (var i = 0; i < 4; i++)
        {
            db.TransaccionesEcoToken.Add(new TransaccionEcoToken
            {
                UniversidadId = conductor.UniversidadId!.Value,
                UsuarioId = conductor.Id,
                Tipo = TipoTransaccionEcoToken.ViajeCompletadoConductor,
                Monto = 8,
                IdFuente = $"seed-streak-{i}",
                CreadoEn = DateTimeOffset.UtcNow.AddHours(-i - 1)
            });
            conductor.BalanceEco += 8;
            conductor.EcoVitalicio += 8;
        }

        await db.SaveChangesAsync();

        var pax = await db.Usuarios.SingleAsync(u => u.Correo == "pax4@utn.local");
        await ResetearEcoAsync(db, pax);
        var viaje = await CrearViajeCompletadoConPaxAsync(db, conductor, pax);
        var motor = new MotorEcoTokens(db);

        await motor.AlCompletarViajeAsync(viaje.Id);
        await motor.AlCompletarViajeAsync(viaje.Id); // replay no segunda racha

        var rachas = await db.TransaccionesEcoToken
            .Where(t => t.UsuarioId == conductor.Id
                        && t.Tipo == TipoTransaccionEcoToken.RachaSemanal)
            .ToListAsync();

        Assert.Single(rachas);
        Assert.Equal(10, rachas[0].Monto);
        Assert.Equal(claveSemana, rachas[0].IdFuente);

        await db.Entry(conductor).ReloadAsync();
        Assert.Equal(8 * 5 + 10, conductor.BalanceEco);
        Assert.Equal(
            await db.TransaccionesEcoToken.Where(t => t.UsuarioId == conductor.Id).SumAsync(t => t.Monto),
            conductor.BalanceEco);
    }

    [Fact]
    public async Task Cancelacion_tardia_con_balance_2_registra_menos_2_vitalicio_igual()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver3@utn.local");
        await ResetearEcoAsync(db, conductor);
        conductor.BalanceEco = 2;
        conductor.EcoVitalicio = 50;
        await db.SaveChangesAsync();

        var viajeId = Guid.NewGuid();
        var motor = new MotorEcoTokens(db);

        await motor.AlCancelacionTardiaAsync(viajeId, conductor.Id);
        await motor.AlCancelacionTardiaAsync(viajeId, conductor.Id); // replay

        await db.Entry(conductor).ReloadAsync();
        Assert.Equal(0, conductor.BalanceEco);
        Assert.Equal(50, conductor.EcoVitalicio);
        Assert.True(conductor.BalanceEco >= 0);

        var penalties = await db.TransaccionesEcoToken
            .Where(t => t.UsuarioId == conductor.Id
                        && t.Tipo == TipoTransaccionEcoToken.PenalizacionCancelacionTardia
                        && t.IdFuente == viajeId.ToString())
            .ToListAsync();

        Assert.Single(penalties);
        Assert.Equal(-2, penalties[0].Monto);
    }

    [Fact]
    public async Task Gamificacion_off_no_acredita_y_me_eco_devuelve_congelado()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver3@utn.local");
        var pax = await db.Usuarios.SingleAsync(u => u.Correo == "pax4@utn.local");
        await ResetearEcoAsync(db, conductor);
        await ResetearEcoAsync(db, pax);

        var config = await db.ConfiguracionesUniversidad.SingleAsync(c =>
            c.UniversidadId == conductor.UniversidadId);
        config.GamificacionHabilitada = false;
        conductor.BalanceEco = 12;
        conductor.EcoVitalicio = 12;
        await db.SaveChangesAsync();

        var viaje = await CrearViajeCompletadoConPaxAsync(db, conductor, pax);
        var motor = new MotorEcoTokens(db);

        await motor.AlCompletarViajeAsync(viaje.Id);
        await motor.AlCancelacionTardiaAsync(viaje.Id, conductor.Id);

        await db.Entry(conductor).ReloadAsync();
        Assert.Equal(12, conductor.BalanceEco);
        Assert.Equal(12, conductor.EcoVitalicio);
        Assert.False(await db.TransaccionesEcoToken.AnyAsync(t =>
            t.IdFuente == viaje.Id.ToString()));

        var resumen = await motor.ObtenerResumenAsync(conductor.Id);
        Assert.Equal(12, resumen.Balance);
        Assert.Equal(12, resumen.Lifetime);
        Assert.False(resumen.GamificationEnabled);
        Assert.Equal("Bronce", resumen.Level);
    }

    [Theory]
    [InlineData(0, "Bronce", 0.0)]
    [InlineData(50, "Bronce", 0.5)]
    [InlineData(99, "Bronce", 0.99)]
    [InlineData(100, "Plata", 0.0)]
    [InlineData(300, "Plata", 0.5)]
    [InlineData(500, "Oro", 0.0)]
    [InlineData(1250, "Oro", 0.5)]
    [InlineData(2000, "Platino", null)]
    [InlineData(5000, "Platino", null)]
    public void Umbrales_nivel_y_formula_progreso(int vitalicio, string nivelEsperado, double? progresoEsperado)
    {
        var (level, progress) = NivelesEcoTokens.Calcular(vitalicio);
        Assert.Equal(nivelEsperado, level);
        if (progresoEsperado is null)
        {
            Assert.Null(progress);
        }
        else
        {
            Assert.NotNull(progress);
            Assert.Equal(progresoEsperado.Value, progress.Value, precision: 5);
        }
    }

    private static async Task ResetearEcoAsync(ContextoApp db, Usuario usuario)
    {
        var txs = await db.TransaccionesEcoToken
            .Where(t => t.UsuarioId == usuario.Id)
            .ToListAsync();
        db.TransaccionesEcoToken.RemoveRange(txs);
        usuario.BalanceEco = 0;
        usuario.EcoVitalicio = 0;
        await db.SaveChangesAsync();
    }

    private static async Task<Viaje> CrearViajeCompletadoConPaxAsync(
        ContextoApp db,
        Usuario conductor,
        Usuario pasajero)
    {
        var campus = await db.Sedes.FirstAsync(c => c.Id == conductor.CampusId);
        var ahora = DateTimeOffset.UtcNow;
        var viaje = new Viaje
        {
            UniversidadId = conductor.UniversidadId!.Value,
            ConductorId = conductor.Id,
            CampusDestinoId = campus.Id,
            OrigenTexto = "Origen eco",
            OrigenLat = -0.35,
            OrigenLng = -78.12,
            SaleEn = ahora.AddHours(-1),
            AsientosDisponibles = 2,
            DistanciaKm = 5m,
            Estado = EstadoViaje.Completado,
            IniciadoEn = ahora.AddMinutes(-40),
            CompletadoEn = ahora
        };
        db.Viajes.Add(viaje);
        await db.SaveChangesAsync();

        db.SolicitudesViaje.Add(new SolicitudViaje
        {
            UniversidadId = viaje.UniversidadId,
            ViajeId = viaje.Id,
            PasajeroId = pasajero.Id,
            RecogidaTexto = "Parada",
            RecogidaLat = -0.351,
            RecogidaLng = -78.121,
            Estado = EstadoSolicitudViaje.Aceptada
        });
        await db.SaveChangesAsync();
        return viaje;
    }

    [Fact]
    public async Task Canje_premio_descuenta_balance_y_queda_registrado()
    {
        await using var db = await CrearDbConSeedAsync();
        var pax = await db.Usuarios.SingleAsync(u => u.Correo == "pax4@utn.local");
        await ResetearEcoAsync(db, pax);
        pax.BalanceEco = 100;
        await db.SaveChangesAsync();

        var motor = new MotorEcoTokens(db);
        var resultado = await motor.CanjearPremioAsync(pax.Id, "gorra");

        await db.Entry(pax).ReloadAsync();
        Assert.Equal(60, pax.BalanceEco);
        Assert.Equal(40, resultado.Costo);
        Assert.Equal("gorra", resultado.CodigoPremio);
        Assert.Contains("Gorra", resultado.NombrePremio);

        Assert.Equal(1, await db.TransaccionesEcoToken.CountAsync(t =>
            t.UsuarioId == pax.Id && t.Tipo == TipoTransaccionEcoToken.CanjePremio));
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
}
