using System.Security.Cryptography;
using Kubix.Application.Auth;
using Kubix.Application.SuperAdmin;
using Kubix.Application.Tenancy;
using Kubix.Domain;
using Kubix.Domain.Entities;
using Kubix.Domain.Enums;
using Kubix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kubix.Infrastructure.SuperAdmin;

public sealed class ServicioSuperAdmin(
    ContextoApp db,
    IEscritorAuditoria auditoria,
    ICacheEstadoUsuario cacheEstado) : IServicioSuperAdmin
{
    public async Task<IReadOnlyList<UniversidadResumenDto>> ListarUniversidadesAsync(
        CancellationToken ct = default)
    {
        var filas = await db.Universidades
            .AsNoTracking()
            .OrderBy(u => u.Nombre)
            .Select(u => new
            {
                u.Id,
                u.Nombre,
                u.Slug,
                u.Estado,
                CantidadCampuses = u.Sedes.Count,
                CantidadUsuarios = u.Usuarios.Count(x => x.Estado != EstadoUsuario.Eliminado)
            })
            .ToListAsync(ct);

        return filas.Select(u => new UniversidadResumenDto
        {
            Id = u.Id,
            Nombre = u.Nombre,
            Slug = u.Slug,
            Estado = ConversorEnumDominio.ACadenaDb(u.Estado),
            CantidadCampuses = u.CantidadCampuses,
            CantidadUsuarios = u.CantidadUsuarios
        }).ToList();
    }

    public async Task<UniversidadDetalleDto> CrearUniversidadAsync(
        SolicitudCrearUniversidad solicitud,
        CancellationToken ct = default)
    {
        var nombre = (solicitud.Nombre ?? string.Empty).Trim();
        var slug = NormalizarSlug(solicitud.Slug);
        var dominio = NormalizarDominio(solicitud.DominioCorreoPermitido);

        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw ExcepcionSuperAdmin.Validacion("Name is required.");
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            throw ExcepcionSuperAdmin.Validacion("Slug is required.");
        }

        if (await db.Universidades.AnyAsync(u => u.Slug == slug, ct))
        {
            throw ExcepcionSuperAdmin.Conflicto("Slug already exists.", "slug_taken");
        }

        var ahora = DateTimeOffset.UtcNow;
        var universidad = new Universidad
        {
            Nombre = nombre,
            Slug = slug,
            Estado = EstadoUniversidad.Activa,
            CreadoEn = ahora,
            ActualizadoEn = ahora
        };

        var configuracion = new ConfiguracionUniversidad
        {
            UniversidadId = universidad.Id,
            DominioCorreoPermitido = dominio,
            ZonaHoraria = "America/Guayaquil",
            CorreoSoporte = dominio is null ? "soporte@kubix.local" : $"soporte@{dominio}",
            MaxViajesDiariosPorConductor = 6,
            CalificacionMinimaConductor = 3.5m,
            FactorCo2KgKm = 0.21m,
            GamificacionHabilitada = true,
            SeguimientoCo2Habilitado = true,
            NotificarSos = true,
            NotificarBloqueo = true,
            NotificarReporteSemanal = true,
            CreadoEn = ahora,
            ActualizadoEn = ahora
        };

        db.Universidades.Add(universidad);
        db.ConfiguracionesUniversidad.Add(configuracion);
        await db.SaveChangesAsync(ct);

        await auditoria.EscribirAsync(
            "university.created",
            TipoEventoAuditoria.Admin,
            SeveridadAuditoria.Media,
            universidadId: universidad.Id,
            ct: ct);

        return MapearDetalle(universidad, configuracion);
    }

    public async Task<UniversidadDetalleDto> ActualizarUniversidadAsync(
        Guid id,
        SolicitudActualizarUniversidad solicitud,
        CancellationToken ct = default)
    {
        var nombre = (solicitud.Nombre ?? string.Empty).Trim();
        var slug = NormalizarSlug(solicitud.Slug);

        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw ExcepcionSuperAdmin.Validacion("Name is required.");
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            throw ExcepcionSuperAdmin.Validacion("Slug is required.");
        }

        var universidad = await db.Universidades
            .Include(u => u.Configuracion)
            .FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw ExcepcionSuperAdmin.NoEncontrado("University not found.");

        if (await db.Universidades.AnyAsync(u => u.Slug == slug && u.Id != id, ct))
        {
            throw ExcepcionSuperAdmin.Conflicto("Slug already exists.", "slug_taken");
        }

        universidad.Nombre = nombre;
        universidad.Slug = slug;
        universidad.ActualizadoEn = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return MapearDetalle(universidad, universidad.Configuracion);
    }

    public async Task SuspenderUniversidadAsync(Guid id, CancellationToken ct = default)
    {
        var universidad = await db.Universidades.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw ExcepcionSuperAdmin.NoEncontrado("University not found.");

        if (universidad.Estado == EstadoUniversidad.Suspendida)
        {
            return;
        }

        universidad.Estado = EstadoUniversidad.Suspendida;
        universidad.ActualizadoEn = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        await cacheEstado.InvalidarUniversidadAsync(id, ct);

        await auditoria.EscribirAsync(
            "university.suspended",
            TipoEventoAuditoria.Admin,
            SeveridadAuditoria.Alta,
            universidadId: id,
            ct: ct);
    }

    public async Task<IReadOnlyList<CampusDto>> ListarCampusesAsync(
        Guid universidadId,
        CancellationToken ct = default)
    {
        await AsegurarUniversidadExisteAsync(universidadId, ct);

        var sedes = await db.Sedes
            .AsNoTracking()
            .Where(c => c.UniversidadId == universidadId)
            .OrderBy(c => c.Nombre)
            .ToListAsync(ct);

        return sedes.Select(MapearCampus).ToList();
    }

    public async Task<CampusDto> CrearCampusAsync(
        Guid universidadId,
        SolicitudCrearCampus solicitud,
        CancellationToken ct = default)
    {
        await AsegurarUniversidadExisteAsync(universidadId, ct);

        var nombre = (solicitud.Nombre ?? string.Empty).Trim();
        var direccion = (solicitud.Direccion ?? string.Empty).Trim();
        ValidarCampus(nombre, direccion, solicitud.Lat, solicitud.Lng);

        if (await db.Sedes.AnyAsync(c => c.UniversidadId == universidadId && c.Nombre == nombre, ct))
        {
            throw ExcepcionSuperAdmin.Conflicto("Campus name already exists.", "campus_name_taken");
        }

        var ahora = DateTimeOffset.UtcNow;
        var campus = new Campus
        {
            UniversidadId = universidadId,
            Nombre = nombre,
            Direccion = direccion,
            Lat = solicitud.Lat,
            Lng = solicitud.Lng,
            CreadoEn = ahora,
            ActualizadoEn = ahora
        };

        db.Sedes.Add(campus);
        await db.SaveChangesAsync(ct);

        return MapearCampus(campus);
    }

    public async Task<CampusDto> ActualizarCampusAsync(
        Guid universidadId,
        Guid campusId,
        SolicitudActualizarCampus solicitud,
        CancellationToken ct = default)
    {
        var campus = await db.Sedes.FirstOrDefaultAsync(
            c => c.Id == campusId && c.UniversidadId == universidadId,
            ct) ?? throw ExcepcionSuperAdmin.NoEncontrado("Campus not found.");

        var nombre = (solicitud.Nombre ?? string.Empty).Trim();
        var direccion = (solicitud.Direccion ?? string.Empty).Trim();
        ValidarCampus(nombre, direccion, solicitud.Lat, solicitud.Lng);

        if (await db.Sedes.AnyAsync(
                c => c.UniversidadId == universidadId && c.Nombre == nombre && c.Id != campusId,
                ct))
        {
            throw ExcepcionSuperAdmin.Conflicto("Campus name already exists.", "campus_name_taken");
        }

        campus.Nombre = nombre;
        campus.Direccion = direccion;
        campus.Lat = solicitud.Lat;
        campus.Lng = solicitud.Lng;
        campus.ActualizadoEn = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return MapearCampus(campus);
    }

    public async Task EliminarCampusAsync(
        Guid universidadId,
        Guid campusId,
        CancellationToken ct = default)
    {
        var campus = await db.Sedes.FirstOrDefaultAsync(
            c => c.Id == campusId && c.UniversidadId == universidadId,
            ct) ?? throw ExcepcionSuperAdmin.NoEncontrado("Campus not found.");

        var tieneUsuarios = await db.Usuarios.AnyAsync(u => u.CampusId == campusId, ct);
        if (tieneUsuarios)
        {
            throw ExcepcionSuperAdmin.Conflicto(
                "Campus has assigned users and cannot be deleted.",
                "campus_has_users");
        }

        db.Sedes.Remove(campus);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CoordinadorDto>> ListarCoordinadoresAsync(
        Guid universidadId,
        CancellationToken ct = default)
    {
        await AsegurarUniversidadExisteAsync(universidadId, ct);

        var filas = await db.Usuarios
            .AsNoTracking()
            .Where(u => u.UniversidadId == universidadId && u.Rol == RolUsuario.Coordinador)
            .OrderBy(u => u.Nombre)
            .ToListAsync(ct);

        return filas.Select(u => new CoordinadorDto
        {
            Id = u.Id,
            Nombre = u.Nombre,
            Correo = u.Correo,
            Estado = ConversorEnumDominio.ACadenaDb(u.Estado),
            DebeCambiarContrasena = u.DebeCambiarContrasena,
            UniversidadId = u.UniversidadId
        }).ToList();
    }

    public async Task<CoordinadorCreadoDto> CrearCoordinadorAsync(
        Guid universidadId,
        SolicitudCrearCoordinador solicitud,
        CancellationToken ct = default)
    {
        await AsegurarUniversidadExisteAsync(universidadId, ct);

        var nombre = (solicitud.Nombre ?? string.Empty).Trim();
        var correo = (solicitud.Correo ?? string.Empty).Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw ExcepcionSuperAdmin.Validacion("Name is required.");
        }

        if (string.IsNullOrWhiteSpace(correo) || !correo.Contains('@'))
        {
            throw ExcepcionSuperAdmin.Validacion("A valid email is required.");
        }

        if (await db.Usuarios.AnyAsync(u => u.Correo == correo && u.UniversidadId == universidadId, ct))
        {
            throw ExcepcionSuperAdmin.Conflicto("Email already exists in this university.", "email_taken");
        }

        var temporal = GenerarContrasenaTemporal();
        var ahora = DateTimeOffset.UtcNow;
        var usuario = new Usuario
        {
            UniversidadId = universidadId,
            CampusId = null,
            Rol = RolUsuario.Coordinador,
            Estado = EstadoUsuario.Activo,
            Nombre = nombre,
            Correo = correo,
            HashContrasena = BCrypt.Net.BCrypt.HashPassword(temporal),
            DebeCambiarContrasena = true,
            CreadoEn = ahora,
            ActualizadoEn = ahora
        };

        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync(ct);

        await auditoria.EscribirAsync(
            "coordinador.created",
            TipoEventoAuditoria.Admin,
            SeveridadAuditoria.Media,
            universidadId: universidadId,
            usuarioId: usuario.Id,
            ct: ct);

        return new CoordinadorCreadoDto
        {
            Id = usuario.Id,
            Nombre = usuario.Nombre,
            Correo = usuario.Correo,
            Estado = ConversorEnumDominio.ACadenaDb(usuario.Estado),
            DebeCambiarContrasena = usuario.DebeCambiarContrasena,
            UniversidadId = usuario.UniversidadId,
            ContrasenaTemporal = temporal
        };
    }

    public async Task<RespuestaResetContrasena> ResetearContrasenaCoordinadorAsync(
        Guid coordinadorId,
        CancellationToken ct = default)
    {
        var usuario = await db.Usuarios.FirstOrDefaultAsync(
            u => u.Id == coordinadorId && u.Rol == RolUsuario.Coordinador,
            ct) ?? throw ExcepcionSuperAdmin.NoEncontrado("Coordinador not found.");

        var temporal = GenerarContrasenaTemporal();
        usuario.HashContrasena = BCrypt.Net.BCrypt.HashPassword(temporal);
        usuario.DebeCambiarContrasena = true;
        usuario.ActualizadoEn = DateTimeOffset.UtcNow;

        var ahora = DateTimeOffset.UtcNow;
        var tokens = await db.TokensRefresco
            .Where(t => t.UsuarioId == coordinadorId && t.RevocadoEn == null)
            .ToListAsync(ct);
        foreach (var token in tokens)
        {
            token.RevocadoEn = ahora;
        }

        await db.SaveChangesAsync(ct);
        cacheEstado.Invalidar(coordinadorId);

        await auditoria.EscribirAsync(
            "coordinador.password_reset",
            TipoEventoAuditoria.Admin,
            SeveridadAuditoria.Media,
            universidadId: usuario.UniversidadId,
            usuarioId: usuario.Id,
            ct: ct);

        return new RespuestaResetContrasena
        {
            Id = usuario.Id,
            Correo = usuario.Correo,
            ContrasenaTemporal = temporal,
            DebeCambiarContrasena = true
        };
    }

    public async Task<StatsSuperAdminDto> ObtenerStatsAsync(CancellationToken ct = default)
    {
        var inicioDiaUtc = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        var finDiaUtc = inicioDiaUtc.AddDays(1);

        var universidades = await db.Universidades.CountAsync(ct);
        var usuarios = await db.Usuarios.CountAsync(
            u => u.Rol != RolUsuario.SuperAdministrador
                 && u.Estado != EstadoUsuario.Eliminado,
            ct);
        var viajesHoy = await db.Viajes.CountAsync(
            v => (v.SaleEn >= inicioDiaUtc && v.SaleEn < finDiaUtc)
                 || (v.CreadoEn >= inicioDiaUtc && v.CreadoEn < finDiaUtc),
            ct);
        var sosActivos = await db.AlertasSos.CountAsync(a => a.Estado == EstadoAlertaSos.Activa, ct);

        return new StatsSuperAdminDto
        {
            CantidadUniversidades = universidades,
            TotalUsuarios = usuarios,
            ViajesHoy = viajesHoy,
            CantidadSosActivos = sosActivos
        };
    }

    private async Task AsegurarUniversidadExisteAsync(Guid universidadId, CancellationToken ct)
    {
        if (!await db.Universidades.AnyAsync(u => u.Id == universidadId, ct))
        {
            throw ExcepcionSuperAdmin.NoEncontrado("University not found.");
        }
    }

    private static void ValidarCampus(string nombre, string direccion, double lat, double lng)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw ExcepcionSuperAdmin.Validacion("Name is required.");
        }

        if (string.IsNullOrWhiteSpace(direccion))
        {
            throw ExcepcionSuperAdmin.Validacion("Address is required.");
        }

        if (lat is < -90 or > 90 || lng is < -180 or > 180)
        {
            throw ExcepcionSuperAdmin.Validacion("Latitude/longitude out of range.");
        }
    }

    private static UniversidadDetalleDto MapearDetalle(
        Universidad universidad,
        ConfiguracionUniversidad? configuracion) => new()
    {
        Id = universidad.Id,
        Nombre = universidad.Nombre,
        Slug = universidad.Slug,
        Estado = ConversorEnumDominio.ACadenaDb(universidad.Estado),
        DominioCorreoPermitido = configuracion?.DominioCorreoPermitido
    };

    private static CampusDto MapearCampus(Campus campus) => new()
    {
        Id = campus.Id,
        UniversidadId = campus.UniversidadId,
        Nombre = campus.Nombre,
        Direccion = campus.Direccion,
        Lat = campus.Lat,
        Lng = campus.Lng
    };

    private static string NormalizarSlug(string? slug) =>
        (slug ?? string.Empty).Trim().ToLowerInvariant();

    private static string? NormalizarDominio(string? dominio)
    {
        var valor = (dominio ?? string.Empty).Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(valor) ? null : valor;
    }

    private static string GenerarContrasenaTemporal()
    {
        const string alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$";
        Span<char> chars = stackalloc char[14];
        var bytes = RandomNumberGenerator.GetBytes(chars.Length);
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = alfabeto[bytes[i] % alfabeto.Length];
        }

        return new string(chars);
    }
}
