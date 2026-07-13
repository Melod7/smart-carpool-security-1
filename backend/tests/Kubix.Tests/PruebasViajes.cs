using Kubix.Application.Tenancy;
using Kubix.Application.Viajes;
using Kubix.Domain.Enums;
using Kubix.Infrastructure.Persistence;
using Kubix.Infrastructure.Seeding;
using Kubix.Infrastructure.Tenancy;
using Kubix.Infrastructure.Viajes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kubix.Tests;

public class PruebasViajes
{
    [Fact]
    public async Task Publicar_sobre_limite_diario_devuelve_422()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver1@utn.local");
        var campus = await db.Sedes.FirstAsync(c => c.UniversidadId == conductor.UniversidadId);
        var config = await db.ConfiguracionesUniversidad.SingleAsync(c => c.UniversidadId == conductor.UniversidadId);
        config.MaxViajesDiariosPorConductor = 1;
        await db.SaveChangesAsync();

        // Día fijo lejos del seed para no depender de la hora UTC.
        var saleEn = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(10).AddHours(9), TimeSpan.Zero);
        db.Viajes.Add(new Domain.Entities.Viaje
        {
            UniversidadId = conductor.UniversidadId!.Value,
            ConductorId = conductor.Id,
            CampusDestinoId = campus.Id,
            OrigenTexto = "Ya publicado",
            OrigenLat = -0.35,
            OrigenLng = -78.12,
            SaleEn = saleEn,
            AsientosDisponibles = 1,
            DistanciaKm = 5m,
            Estado = EstadoViaje.Programado
        });
        await db.SaveChangesAsync();

        var directions = new DirectionsMock
        {
            Resultado = new ResultadoDirections { Polilinea = "abc", DistanciaKm = 4.2m }
        };
        var servicio = CrearServicio(db, conductor, directions);

        var ex = await Assert.ThrowsAsync<ExcepcionViajes>(() =>
            servicio.PublicarViajeAsync(conductor.Id, new SolicitudPublicarViaje
            {
                OrigenTexto = "Extra",
                OrigenLat = -0.35,
                OrigenLng = -78.12,
                CampusDestinoId = campus.Id,
                SaleEn = saleEn.AddHours(2),
                AsientosDisponibles = 2
            }));

        Assert.Equal(422, ex.CodigoEstado);
        Assert.Equal("max_daily_trips", ex.Codigo);
    }

    [Fact]
    public async Task Directions_exito_persiste_polyline_y_distancia()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver2@utn.local");
        var campus = await db.Sedes.FirstAsync(c => c.Id == conductor.CampusId);

        const string polilinea = "_p~iF~ps|U_ulLnnqC_mqNvxq`@";
        var directions = new DirectionsMock
        {
            Resultado = new ResultadoDirections { Polilinea = polilinea, DistanciaKm = 12.345m }
        };
        var servicio = CrearServicio(db, conductor, directions);

        var publicado = await servicio.PublicarViajeAsync(conductor.Id, new SolicitudPublicarViaje
        {
            OrigenTexto = "Norte Ibarra",
            OrigenLat = -0.33,
            OrigenLng = -78.11,
            CampusDestinoId = campus.Id,
            SaleEn = DateTimeOffset.UtcNow.AddDays(1).AddHours(7),
            AsientosDisponibles = 2
        });

        Assert.Equal("scheduled", publicado.Estado);
        Assert.Equal(polilinea, publicado.Polilinea);
        Assert.Equal(12.345m, publicado.DistanciaKm);

        var enDb = await db.Viajes.SingleAsync(v => v.Id == publicado.Id);
        Assert.Equal(polilinea, enDb.Polilinea);
        Assert.Equal(12.345m, enDb.DistanciaKm);
    }

    [Fact]
    public async Task Directions_fallo_crea_viaje_con_haversine()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver2@utn.local");
        var campus = await db.Sedes.FirstAsync(c => c.Id == conductor.CampusId);

        var directions = new DirectionsMock
        {
            Excepcion = new InvalidOperationException("quota exceeded")
        };
        var servicio = CrearServicio(db, conductor, directions);

        const double origenLat = -0.33;
        const double origenLng = -78.11;
        var esperado = UtilidadHaversine.DistanciaKm(origenLat, origenLng, campus.Lat, campus.Lng);

        var publicado = await servicio.PublicarViajeAsync(conductor.Id, new SolicitudPublicarViaje
        {
            OrigenTexto = "Sur Ibarra",
            OrigenLat = origenLat,
            OrigenLng = origenLng,
            CampusDestinoId = campus.Id,
            SaleEn = DateTimeOffset.UtcNow.AddDays(2).AddHours(7),
            AsientosDisponibles = 1
        });

        Assert.Equal("scheduled", publicado.Estado);
        Assert.Null(publicado.Polilinea);
        Assert.True(publicado.DistanciaKm > 0);
        Assert.Equal(esperado, publicado.DistanciaKm);
    }

    [Fact]
    public async Task Origen_invalido_devuelve_422()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver1@utn.local");
        var campus = await db.Sedes.FirstAsync(c => c.Id == conductor.CampusId);

        var directions = new DirectionsMock
        {
            Excepcion = new HttpRequestException("Directions down")
        };
        var servicio = CrearServicio(db, conductor, directions);

        var ex = await Assert.ThrowsAsync<ExcepcionViajes>(() =>
            servicio.PublicarViajeAsync(conductor.Id, new SolicitudPublicarViaje
            {
                OrigenTexto = "Fuera de rango",
                OrigenLat = 95,
                OrigenLng = -78.12,
                CampusDestinoId = campus.Id,
                SaleEn = DateTimeOffset.UtcNow.AddDays(3),
                AsientosDisponibles = 1
            }));

        Assert.Equal(422, ex.CodigoEstado);
        Assert.Equal("invalid_origin", ex.Codigo);
    }

    [Fact]
    public async Task Pasajero_publicar_devuelve_403()
    {
        await using var db = await CrearDbConSeedAsync();
        var pasajero = await db.Usuarios.SingleAsync(u => u.Correo == "pax1@utn.local");
        var campus = await db.Sedes.FirstAsync(c => c.Id == pasajero.CampusId);

        var servicio = CrearServicio(db, pasajero, new DirectionsMock());

        var ex = await Assert.ThrowsAsync<ExcepcionViajes>(() =>
            servicio.PublicarViajeAsync(pasajero.Id, new SolicitudPublicarViaje
            {
                OrigenTexto = "No permitido",
                OrigenLat = -0.35,
                OrigenLng = -78.12,
                CampusDestinoId = campus.Id,
                SaleEn = DateTimeOffset.UtcNow.AddDays(1),
                AsientosDisponibles = 1
            }));

        Assert.Equal(403, ex.CodigoEstado);
        Assert.Equal("driver_only", ex.Codigo);
    }

    [Fact]
    public async Task Vehiculo_get_put_round_trip()
    {
        await using var db = await CrearDbConSeedAsync();
        var conductor = await db.Usuarios.SingleAsync(u => u.Correo == "driver3@utn.local");
        var servicio = CrearServicio(db, conductor, new DirectionsMock());

        var actualizado = await servicio.UpsertVehiculoAsync(conductor.Id, new SolicitudUpsertVehiculo
        {
            MarcaModelo = "Mazda 3",
            Placa = "PBA-7777",
            Color = "Rojo",
            AsientosTotales = 4
        });

        Assert.Equal("Mazda 3", actualizado.MarcaModelo);
        Assert.Equal("PBA-7777", actualizado.Placa);
        Assert.Equal("Rojo", actualizado.Color);
        Assert.Equal(4, actualizado.AsientosTotales);
        Assert.Equal(conductor.UniversidadId, actualizado.UniversidadId);

        var leido = await servicio.ObtenerVehiculoAsync(conductor.Id);
        Assert.Equal(actualizado.Id, leido.Id);
        Assert.Equal("Mazda 3", leido.MarcaModelo);
        Assert.Equal("PBA-7777", leido.Placa);
        Assert.Equal(4, leido.AsientosTotales);
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
        return new ServicioViajes(db, directions, auditoria);
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
        return new ContextoApp(opciones, new ContextoInquilino { OmitirFiltros = true });
    }

    private sealed class DirectionsMock : IServicioDirections
    {
        public ResultadoDirections? Resultado { get; set; }
        public Exception? Excepcion { get; set; }

        public Task<ResultadoDirections?> ObtenerRutaAsync(
            double origenLat,
            double origenLng,
            double destinoLat,
            double destinoLng,
            CancellationToken ct = default)
        {
            if (Excepcion is not null)
            {
                throw Excepcion;
            }

            return Task.FromResult(Resultado);
        }
    }
}
