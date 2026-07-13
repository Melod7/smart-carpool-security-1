using Kubix.Application.Sos;
using Kubix.Domain.Entities;
using Kubix.Domain.Enums;
using Kubix.Infrastructure.Persistence;
using Kubix.Infrastructure.Seeding;
using Kubix.Infrastructure.Sos;
using Kubix.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kubix.Tests;

public class PruebasSos
{
    [Fact]
    public async Task Crear_alerta_visible_para_coordinador_misma_universidad()
    {
        await using var db = await CrearDbConSeedAsync();
        var pax = await db.Usuarios.SingleAsync(u => u.Correo == "pax1@utn.local");
        var coord = await db.Usuarios.SingleAsync(u => u.Correo == "coordinador@utn.local");
        var viaje = await db.Viajes.FirstAsync(v =>
            v.UniversidadId == pax.UniversidadId && v.Estado == EstadoViaje.EnCurso);

        var servicioPax = CrearServicio(db, pax);
        var creada = await servicioPax.CrearAsync(
            pax.Id,
            new SolicitudCrearSos
            {
                Lat = -0.36,
                Lng = -78.14,
                ViajeId = viaje.Id
            });

        Assert.Equal("active", creada.Estado);
        Assert.Equal(viaje.Id, creada.ViajeId);

        var auditoria = await db.EventosAuditoria
            .Where(e => e.Accion == "sos.fired" && e.UsuarioId == pax.Id)
            .OrderByDescending(e => e.CreadoEn)
            .FirstAsync();
        Assert.Equal(SeveridadAuditoria.Alta, auditoria.Severidad);
        Assert.Equal(TipoEventoAuditoria.Sos, auditoria.Tipo);

        var notificacion = await db.Notificaciones
            .Where(n =>
                n.Tipo == TipoNotificacion.Sos &&
                n.UniversidadId == pax.UniversidadId &&
                n.Cuerpo.Contains(pax.Nombre))
            .OrderByDescending(n => n.CreadoEn)
            .FirstOrDefaultAsync();
        Assert.NotNull(notificacion);
        Assert.Equal(RolUsuario.Coordinador, notificacion!.RolDestinatario);

        var servicioCoord = CrearServicio(db, coord);
        var lista = await servicioCoord.ListarAdminAsync();
        var item = Assert.Single(lista, a => a.Id == creada.Id);
        Assert.Equal(pax.Id, item.Estudiante.Id);
        Assert.Equal(pax.Nombre, item.Estudiante.Nombre);
        Assert.NotNull(item.Viaje);
        Assert.Equal(viaje.Id, item.Viaje!.Id);
        Assert.NotNull(item.Conductor);
        Assert.Equal(viaje.ConductorId, item.Conductor!.Id);
        Assert.Equal(-0.36, item.Lat);
        Assert.Equal(-78.14, item.Lng);
    }

    [Fact]
    public async Task Coordinador_otra_universidad_no_ve_alerta()
    {
        await using var db = await CrearDbConSeedAsync();
        var paxUtn = await db.Usuarios.SingleAsync(u => u.Correo == "pax1@utn.local");
        var coordPuce = await db.Usuarios.SingleAsync(u => u.Correo == "coordinador@puce.local");

        var servicioPax = CrearServicio(db, paxUtn);
        var creada = await servicioPax.CrearAsync(
            paxUtn.Id,
            new SolicitudCrearSos { Lat = -0.35, Lng = -78.12 });

        var servicioPuce = CrearServicio(db, coordPuce);
        var lista = await servicioPuce.ListarAdminAsync();
        Assert.DoesNotContain(lista, a => a.Id == creada.Id);
    }

    [Fact]
    public async Task Owner_cierra_alerta_con_resolved_by_owner()
    {
        await using var db = await CrearDbConSeedAsync();
        var pax = await db.Usuarios.SingleAsync(u => u.Correo == "pax2@utn.local");

        var servicio = CrearServicio(db, pax);
        var creada = await servicio.CrearAsync(
            pax.Id,
            new SolicitudCrearSos { Lat = -0.351, Lng = -78.121 });

        var cerrada = await servicio.CerrarAsync(pax.Id, creada.Id);
        Assert.Equal("resolved", cerrada.Estado);
        Assert.Equal(pax.Id, cerrada.ResueltaPor);
        Assert.NotNull(cerrada.ResueltaEn);

        var enDb = await db.AlertasSos.SingleAsync(a => a.Id == creada.Id);
        Assert.Equal(EstadoAlertaSos.Resuelta, enDb.Estado);
        Assert.Equal(pax.Id, enDb.ResueltaPor);
        Assert.NotNull(enDb.ResueltaEn);

        var coord = await db.Usuarios.SingleAsync(u => u.Correo == "coordinador@utn.local");
        var lista = await CrearServicio(db, coord).ListarAdminAsync();
        var item = Assert.Single(lista, a => a.Id == creada.Id);
        Assert.Equal("resolved", item.Estado);
        Assert.Equal(pax.Id, item.ResueltaPor);
    }

    [Fact]
    public async Task Cerrar_alerta_de_otro_devuelve_404()
    {
        await using var db = await CrearDbConSeedAsync();
        var owner = await db.Usuarios.SingleAsync(u => u.Correo == "pax1@utn.local");
        var otro = await db.Usuarios.SingleAsync(u => u.Correo == "pax2@utn.local");

        var creada = await CrearServicio(db, owner).CrearAsync(
            owner.Id,
            new SolicitudCrearSos { Lat = -0.352, Lng = -78.122 });

        var ex = await Assert.ThrowsAsync<ExcepcionSos>(() =>
            CrearServicio(db, otro).CerrarAsync(otro.Id, creada.Id));

        Assert.Equal(404, ex.CodigoEstado);
        Assert.Equal("sos_not_found", ex.Codigo);
    }

    [Fact]
    public async Task Disparar_sin_viaje_funciona()
    {
        await using var db = await CrearDbConSeedAsync();
        var driver = await db.Usuarios.SingleAsync(u => u.Correo == "driver1@utn.local");

        var creada = await CrearServicio(db, driver).CrearAsync(
            driver.Id,
            new SolicitudCrearSos { Lat = -0.34, Lng = -78.11 });

        Assert.Equal("active", creada.Estado);
        Assert.Null(creada.ViajeId);

        var coord = await db.Usuarios.SingleAsync(u => u.Correo == "coordinador@utn.local");
        var lista = await CrearServicio(db, coord).ListarAdminAsync();
        var item = Assert.Single(lista, a => a.Id == creada.Id);
        Assert.Null(item.Viaje);
        Assert.Null(item.Conductor);
        Assert.Equal(driver.Id, item.Estudiante.Id);
    }

    [Fact]
    public async Task Coordinador_resuelve_con_resolved_by_coordinador()
    {
        await using var db = await CrearDbConSeedAsync();
        var pax = await db.Usuarios.SingleAsync(u => u.Correo == "pax3@utn.local");
        var coord = await db.Usuarios.SingleAsync(u => u.Correo == "coordinador@utn.local");

        var creada = await CrearServicio(db, pax).CrearAsync(
            pax.Id,
            new SolicitudCrearSos { Lat = -0.353, Lng = -78.123 });

        var resuelta = await CrearServicio(db, coord).ResolverAdminAsync(coord.Id, creada.Id);
        Assert.Equal("resolved", resuelta.Estado);
        Assert.Equal(coord.Id, resuelta.ResueltaPor);
        Assert.NotNull(resuelta.ResueltaEn);

        var auditoria = await db.EventosAuditoria
            .Where(e => e.Accion == "sos.resolved" && e.UsuarioId == coord.Id)
            .OrderByDescending(e => e.CreadoEn)
            .FirstAsync();
        Assert.Equal(SeveridadAuditoria.Alta, auditoria.Severidad);
    }

    private static IServicioSos CrearServicio(ContextoApp db, Usuario usuario)
    {
        if (db is not ContextoAppConInquilino mutable)
        {
            throw new InvalidOperationException("Se espera ContextoAppConInquilino en las pruebas SOS.");
        }

        mutable.Inquilino.OmitirFiltros = false;
        mutable.Inquilino.UniversidadId = usuario.UniversidadId;
        mutable.Inquilino.CampusId = usuario.CampusId;
        mutable.Inquilino.UsuarioId = usuario.Id;
        mutable.Inquilino.Rol = usuario.Rol;
        return new ServicioSos(db, new EscritorAuditoria(db, mutable.Inquilino), mutable.Inquilino);
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

    /// <summary>
    /// Expone el <see cref="ContextoInquilino"/> mutable para cambiar el tenant entre llamadas.
    /// </summary>
    private sealed class ContextoAppConInquilino(
        DbContextOptions<ContextoApp> options,
        ContextoInquilino inquilino) : ContextoApp(options, inquilino)
    {
        public ContextoInquilino Inquilino { get; } = inquilino;
    }
}
