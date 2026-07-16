using Kubix.Application.Tenancy;
using Kubix.Application.Viajes;
using Kubix.Domain.Enums;
using Kubix.Infrastructure.EcoTokens;
using Kubix.Infrastructure.Persistence;
using Kubix.Infrastructure.Seeding;
using Kubix.Infrastructure.Tenancy;
using Kubix.Infrastructure.Viajes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kubix.Tests;

public class PruebasWaypoints
{
    [Fact]
    public async Task Publicar_con_3_waypoints_persiste_seq_y_origen_wp0()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver2@utn.local");
        var campus = await db.Sedes.FirstAsync(c => c.Id == conductor.CampusId);

        var directions = new DirectionsMock
        {
            Resultado = new ResultadoDirections { Polilinea = "poly", DistanciaKm = 10m }
        };
        var servicio = CrearServicio(db, conductor, directions);

        var publicado = await servicio.PublicarViajeAsync(conductor.Id, new SolicitudPublicarViaje
        {
            Waypoints =
            [
                new WaypointDto { Lat = -0.34, Lng = -78.13, Etiqueta = "Inicio" },
                new WaypointDto { Lat = -0.345, Lng = -78.128 },
                new WaypointDto { Lat = -0.35, Lng = -78.125, Etiqueta = "Cerca campus" }
            ],
            CampusDestinoId = campus.Id,
            SaleEn = DateTimeOffset.UtcNow.AddDays(1).AddHours(8),
            AsientosDisponibles = 2
        });

        Assert.Equal(-0.34, publicado.OrigenLat);
        Assert.Equal(-78.13, publicado.OrigenLng);
        Assert.Equal("Inicio", publicado.OrigenTexto);
        Assert.Equal(3, publicado.Waypoints.Count);
        Assert.Equal(new int[] { 0, 1, 2 }, publicado.Waypoints.Select(w => w.Seq ?? -1).ToArray());

        var enDb = await db.PuntosRutaViaje
            .Where(p => p.ViajeId == publicado.Id)
            .OrderBy(p => p.Seq)
            .ToListAsync();
        Assert.Equal(3, enDb.Count);
        Assert.Equal(0, enDb[0].Seq);
        Assert.Equal(-0.34, enDb[0].Lat);
        Assert.Equal(2, enDb[2].Seq);
    }

    [Fact]
    public async Task Directions_recibe_vias_intermedias()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver2@utn.local");
        var campus = await db.Sedes.FirstAsync(c => c.Id == conductor.CampusId);

        var directions = new DirectionsMock
        {
            Resultado = new ResultadoDirections { Polilinea = "abc", DistanciaKm = 7.5m }
        };
        var servicio = CrearServicio(db, conductor, directions);

        await servicio.PublicarViajeAsync(conductor.Id, new SolicitudPublicarViaje
        {
            Waypoints =
            [
                new WaypointDto { Lat = -0.36, Lng = -78.14 },
                new WaypointDto { Lat = -0.355, Lng = -78.135 },
                new WaypointDto { Lat = -0.352, Lng = -78.13 }
            ],
            CampusDestinoId = campus.Id,
            SaleEn = DateTimeOffset.UtcNow.AddDays(2),
            AsientosDisponibles = 1
        });

        Assert.NotNull(directions.UltimasVias);
        Assert.Equal(2, directions.UltimasVias!.Count);
        Assert.Equal(-0.355, directions.UltimasVias[0].Lat);
        Assert.Equal(-0.352, directions.UltimasVias[1].Lat);
        Assert.Equal(-0.36, directions.UltimoOrigenLat);
        Assert.Equal(campus.Lat, directions.UltimoDestinoLat);
    }

    [Fact]
    public async Task Directions_fallo_polyline_null_waypoints_intactos_distancia_positiva()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver2@utn.local");
        var campus = await db.Sedes.FirstAsync(c => c.Id == conductor.CampusId);

        var directions = new DirectionsMock
        {
            Excepcion = new InvalidOperationException("Directions down")
        };
        var servicio = CrearServicio(db, conductor, directions);

        var wps = new List<WaypointDto>
        {
            new() { Lat = -0.36, Lng = -78.14 },
            new() { Lat = -0.355, Lng = -78.13 },
            new() { Lat = -0.353, Lng = -78.125 }
        };

        var esperado = UtilidadHaversine.DistanciaALoLargoKm(
            wps.Select(w => (w.Lat, w.Lng)).Append((campus.Lat, campus.Lng)).ToList());

        var publicado = await servicio.PublicarViajeAsync(conductor.Id, new SolicitudPublicarViaje
        {
            Waypoints = wps,
            CampusDestinoId = campus.Id,
            SaleEn = DateTimeOffset.UtcNow.AddDays(3),
            AsientosDisponibles = 1
        });

        Assert.Null(publicado.Polilinea);
        Assert.True(publicado.DistanciaKm > 0);
        Assert.Equal(esperado, publicado.DistanciaKm);
        Assert.Equal(3, publicado.Waypoints.Count);

        var enDb = await db.PuntosRutaViaje.CountAsync(p => p.ViajeId == publicado.Id);
        Assert.Equal(3, enDb);
    }

    [Fact]
    public async Task Menos_de_2_waypoints_sin_origen_legacy_devuelve_422()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver1@utn.local");
        var campus = await db.Sedes.FirstAsync(c => c.Id == conductor.CampusId);
        var servicio = CrearServicio(db, conductor, new DirectionsMock());

        var ex = await Assert.ThrowsAsync<ExcepcionViajes>(() =>
            servicio.PublicarViajeAsync(conductor.Id, new SolicitudPublicarViaje
            {
                Waypoints = [new WaypointDto { Lat = -0.35, Lng = -78.12 }],
                CampusDestinoId = campus.Id,
                SaleEn = DateTimeOffset.UtcNow.AddDays(1),
                AsientosDisponibles = 1
            }));

        Assert.Equal(422, ex.CodigoEstado);
        Assert.Equal("invalid_waypoints", ex.Codigo);

        var exVacio = await Assert.ThrowsAsync<ExcepcionViajes>(() =>
            servicio.PublicarViajeAsync(conductor.Id, new SolicitudPublicarViaje
            {
                CampusDestinoId = campus.Id,
                SaleEn = DateTimeOffset.UtcNow.AddDays(1),
                AsientosDisponibles = 1
            }));

        Assert.Equal(422, exVacio.CodigoEstado);
        Assert.Equal("invalid_waypoints", exVacio.Codigo);
    }

    [Fact]
    public async Task SuggestedWait_proyecta_sobre_segmento_y_tooFar_cuando_lejos()
    {
        await using var db = await CrearDbConSeedAsync();
        var pasajero = await db.Usuarios.SingleAsync(u => u.Correo == "pax1@utn.local");
        var viaje = await db.Viajes
            .Include(v => v.PuntosRuta)
            .Include(v => v.CampusDestino)
            .FirstAsync(v => v.Estado == EstadoViaje.Programado && v.UniversidadId == pasajero.UniversidadId);

        Assert.True(viaje.PuntosRuta.Count >= 2);

        var servicio = CrearServicioSolicitudes(db, pasajero);

        var cerca = await servicio.ObtenerPuntoEsperaSugeridoAsync(
            pasajero.Id,
            viaje.Id,
            viaje.OrigenLat + 0.0001,
            viaje.OrigenLng + 0.0001);

        Assert.False(cerca.TooFar);
        Assert.True(cerca.DistanciaM < UtilidadPuntoEspera.UmbralTooFarMetros);
        Assert.True(cerca.SegmentIndex >= 0);

        var lejos = await servicio.ObtenerPuntoEsperaSugeridoAsync(
            pasajero.Id,
            viaje.Id,
            viaje.OrigenLat + 0.05,
            viaje.OrigenLng + 0.05);

        Assert.True(lejos.TooFar);
        Assert.True(lejos.DistanciaM > UtilidadPuntoEspera.UmbralTooFarMetros);

        var disponibles = await servicio.ListarDisponiblesAsync(
            pasajero.Id,
            lat: cerca.Lat,
            lng: cerca.Lng);

        var dto = Assert.Single(disponibles, v => v.Id == viaje.Id);
        Assert.NotNull(dto.EsperaSugerida);
        Assert.NotEmpty(dto.Waypoints);
        Assert.False(dto.EsperaSugerida!.TooFar);
    }

    [Fact]
    public void UtilidadPuntoEspera_proyecta_punto_medio_del_segmento()
    {
        var resultado = UtilidadPuntoEspera.Calcular(
            pasajeroLat: 0.0,
            pasajeroLng: 0.5,
            polilinea: null,
            waypoints: [(0.0, 0.0), (0.0, 1.0)],
            campusLat: 0.0,
            campusLng: 1.0);

        Assert.Equal(0, resultado.SegmentIndex);
        Assert.InRange(resultado.Lat, -0.001, 0.001);
        Assert.InRange(resultado.Lng, 0.49, 0.51);
        Assert.True(resultado.DistanciaM < 50);
        Assert.False(resultado.TooFar);
    }

    private static IServicioViajes CrearServicio(
        ContextoApp db,
        Domain.Entities.Usuario usuario,
        IServicioDirections directions)
    {
        var inquilino = new ContextoInquilino
        {
            OmitirFiltros = false,
            UniversidadId = usuario.UniversidadId,
            CampusId = usuario.CampusId,
            UsuarioId = usuario.Id,
            Rol = usuario.Rol
        };
        var auditoria = new EscritorAuditoria(db, inquilino);
        return new ServicioViajes(db, directions, auditoria, new MotorEcoTokensStub());
    }

    private static IServicioSolicitudesViaje CrearServicioSolicitudes(
        ContextoApp db,
        Domain.Entities.Usuario usuario)
    {
        var inquilino = new ContextoInquilino
        {
            OmitirFiltros = false,
            UniversidadId = usuario.UniversidadId,
            CampusId = usuario.CampusId,
            UsuarioId = usuario.Id,
            Rol = usuario.Rol
        };
        return new ServicioSolicitudesViaje(db, inquilino, new EscritorAuditoria(db, inquilino), new DirectionsMock());
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

    private sealed class DirectionsMock : IServicioDirections
    {
        public ResultadoDirections? Resultado { get; set; }
        public Exception? Excepcion { get; set; }
        public IReadOnlyList<(double Lat, double Lng)>? UltimasVias { get; private set; }
        public double UltimoOrigenLat { get; private set; }
        public double UltimoDestinoLat { get; private set; }

        public Task<ResultadoDirections?> ObtenerRutaAsync(
            double origenLat,
            double origenLng,
            double destinoLat,
            double destinoLng,
            IReadOnlyList<(double Lat, double Lng)>? vias = null,
            CancellationToken ct = default)
        {
            UltimoOrigenLat = origenLat;
            UltimoDestinoLat = destinoLat;
            UltimasVias = vias;

            if (Excepcion is not null)
            {
                throw Excepcion;
            }

            return Task.FromResult(Resultado);
        }
    }
}
