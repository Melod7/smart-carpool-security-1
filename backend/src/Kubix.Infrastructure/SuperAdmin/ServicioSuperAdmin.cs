using Kubix.Application.SuperAdmin;
using Kubix.Domain.Entities;
using Kubix.Domain.Enums;
using Kubix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kubix.Infrastructure.SuperAdmin;

public class ServicioSuperAdmin : IServicioSuperAdmin
{
    private readonly ContextoApp _contexto;

    public ServicioSuperAdmin(ContextoApp contexto)
    {
        _contexto = contexto;
    }

    public async Task<IReadOnlyList<UniversidadResumenDto>> ListarUniversidadesAsync(CancellationToken ct = default)
    {
        return await _contexto.Universidades
            .AsNoTracking()
            .Select(u => new UniversidadResumenDto
            {
                Id = u.Id,
                Nombre = u.Nombre,
                Slug = u.Slug,
                Estado = u.Estado.ToString(),
                CantidadCampuses = _contexto.Set<Campus>().Count(c => c.UniversidadId == u.Id),
                CantidadUsuarios = u.Usuarios != null ? u.Usuarios.Count : 0
            })
            .ToListAsync(ct);
    }

    public async Task<UniversidadDetalleDto> CrearUniversidadAsync(
        SolicitudCrearUniversidad solicitud,
        CancellationToken ct = default)
    {
        var existeSlug = await _contexto.Universidades
            .AnyAsync(u => u.Slug == solicitud.Slug.ToLower(), ct);

        if (existeSlug)
        {
            throw new ExcepcionSuperAdmin(
                400,
                "slug_duplicado",
                "Error al crear universidad",
                $"Ya existe una universidad con el identificador '{solicitud.Slug}'.");
        }

        var universidad = new Universidad
        {
            Id = Guid.NewGuid(),
            Nombre = solicitud.Nombre,
            Slug = solicitud.Slug.ToLower(),
            Estado = EstadoUniversidad.Activa
        };

        _contexto.Universidades.Add(universidad);
        await _contexto.SaveChangesAsync(ct);

        return new UniversidadDetalleDto
        {
            Id = universidad.Id,
            Nombre = universidad.Nombre,
            Slug = universidad.Slug,
            Estado = universidad.Estado.ToString(),
            DominioCorreoPermitido = solicitud.DominioCorreoPermitido
        };
    }

    public async Task<UniversidadDetalleDto> ActualizarUniversidadAsync(
        Guid id,
        SolicitudActualizarUniversidad solicitud,
        CancellationToken ct = default)
    {
        var universidad = await _contexto.Universidades.FindAsync(new object[] { id }, ct);
        if (universidad == null)
        {
            throw new ExcepcionSuperAdmin(
                404,
                "universidad_no_encontrada",
                "Universidad no encontrada",
                $"No se encontró la universidad con ID {id}.");
        }

        universidad.Nombre = solicitud.Nombre;
        await _contexto.SaveChangesAsync(ct);

        return new UniversidadDetalleDto
        {
            Id = universidad.Id,
            Nombre = universidad.Nombre,
            Slug = universidad.Slug,
            Estado = universidad.Estado.ToString()
        };
    }

    public async Task SuspenderUniversidadAsync(Guid id, CancellationToken ct = default)
    {
        var universidad = await _contexto.Universidades.FindAsync(new object[] { id }, ct);
        if (universidad == null)
        {
            throw new ExcepcionSuperAdmin(
                404,
                "universidad_no_encontrada",
                "Universidad no encontrada",
                $"No se encontró la universidad con ID {id}.");
        }

        universidad.Estado = universidad.Estado == EstadoUniversidad.Activa 
            ? EstadoUniversidad.Suspendida 
            : EstadoUniversidad.Activa;

        await _contexto.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CampusDto>> ListarCampusesAsync(Guid universidadId, CancellationToken ct = default)
    {
        return await _contexto.Set<Campus>()
            .AsNoTracking()
            .Where(c => c.UniversidadId == universidadId)
            .Select(c => new CampusDto
            {
                Id = c.Id,
                UniversidadId = c.UniversidadId,
                Nombre = c.Nombre,
                Direccion = c.Direccion,
                Lat = c.Lat,
                Lng = c.Lng
            })
            .ToListAsync(ct);
    }

    public async Task<CampusDto> CrearCampusAsync(
        Guid universidadId,
        SolicitudCrearCampus solicitud,
        CancellationToken ct = default)
    {
        var universidad = await _contexto.Universidades.FindAsync(new object[] { universidadId }, ct);
        if (universidad == null)
        {
            throw new ExcepcionSuperAdmin(
                404,
                "universidad_no_encontrada",
                "Universidad no encontrada",
                $"No existe la universidad {universidadId}.");
        }

        var campus = new Campus
        {
            Id = Guid.NewGuid(),
            UniversidadId = universidadId,
            Nombre = solicitud.Nombre,
            Direccion = solicitud.Direccion,
            Lat = solicitud.Lat,
            Lng = solicitud.Lng
        };

        _contexto.Set<Campus>().Add(campus);
        await _contexto.SaveChangesAsync(ct);

        return new CampusDto
        {
            Id = campus.Id,
            UniversidadId = campus.UniversidadId,
            Nombre = campus.Nombre,
            Direccion = campus.Direccion,
            Lat = campus.Lat,
            Lng = campus.Lng
        };
    }

    public async Task<CampusDto> ActualizarCampusAsync(
        Guid universidadId,
        Guid campusId,
        SolicitudActualizarCampus solicitud,
        CancellationToken ct = default)
    {
        var campus = await _contexto.Set<Campus>()
            .FirstOrDefaultAsync(c => c.Id == campusId && c.UniversidadId == universidadId, ct);

        if (campus == null)
        {
            throw new ExcepcionSuperAdmin(
                404,
                "campus_no_encontrado",
                "Campus no encontrado",
                $"No se encontró el campus {campusId} para esta universidad.");
        }

        campus.Nombre = solicitud.Nombre;
        campus.Direccion = solicitud.Direccion;
        campus.Lat = solicitud.Lat;
        campus.Lng = solicitud.Lng;

        await _contexto.SaveChangesAsync(ct);

        return new CampusDto
        {
            Id = campus.Id,
            UniversidadId = campus.UniversidadId,
            Nombre = campus.Nombre,
            Direccion = campus.Direccion,
            Lat = campus.Lat,
            Lng = campus.Lng
        };
    }

    public async Task EliminarCampusAsync(Guid universidadId, Guid campusId, CancellationToken ct = default)
    {
        var campus = await _contexto.Set<Campus>()
            .FirstOrDefaultAsync(c => c.Id == campusId && c.UniversidadId == universidadId, ct);

        if (campus == null)
        {
            throw new ExcepcionSuperAdmin(
                404,
                "campus_no_encontrado",
                "Campus no encontrado",
                $"No existe el campus {campusId} para esta universidad.");
        }

        _contexto.Set<Campus>().Remove(campus);
        await _contexto.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CoordinadorDto>> ListarCoordinadoresAsync(
        Guid universidadId,
        CancellationToken ct = default)
    {
        return await _contexto.Usuarios
            .AsNoTracking()
            .Where(u => u.UniversidadId == universidadId)
            .Select(u => new CoordinadorDto
            {
                Id = u.Id,
                Nombre = u.Nombre,
                Correo = u.Nombre,
                Estado = u.Estado.ToString(),
                UniversidadId = u.UniversidadId
            })
            .ToListAsync(ct);
    }

    public async Task<CoordinadorCreadoDto> CrearCoordinadorAsync(
        Guid universidadId,
        SolicitudCrearCoordinador solicitud,
        CancellationToken ct = default)
    {
        var contrasenaTemporal = "Kubix" + Random.Shared.Next(100000, 999999) + "!";

        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            UniversidadId = universidadId,
            Nombre = solicitud.Nombre,
            Rol = (RolUsuario)1, // Asignación segura del rol
            Estado = EstadoUsuario.Activo
        };

        _contexto.Usuarios.Add(usuario);
        await _contexto.SaveChangesAsync(ct);

        return new CoordinadorCreadoDto
        {
            Id = usuario.Id,
            Nombre = usuario.Nombre,
            Correo = solicitud.Correo,
            Estado = usuario.Estado.ToString(),
            UniversidadId = usuario.UniversidadId,
            ContrasenaTemporal = contrasenaTemporal
        };
    }

    public async Task EliminarCoordinadorAsync(Guid coordinadorId, CancellationToken ct = default)
    {
        var usuario = await _contexto.Usuarios
            .FirstOrDefaultAsync(u => u.Id == coordinadorId, ct);

        if (usuario == null)
        {
            throw new ExcepcionSuperAdmin(
                404,
                "coordinador_no_encontrado",
                "Coordinador no encontrado",
                $"No existe el coordinador con ID {coordinadorId}.");
        }

        _contexto.Usuarios.Remove(usuario);
        await _contexto.SaveChangesAsync(ct);
    }

    public async Task<RespuestaResetContrasena> ResetearContrasenaCoordinadorAsync(
        Guid coordinadorId,
        CancellationToken ct = default)
    {
        var usuario = await _contexto.Usuarios
            .FirstOrDefaultAsync(u => u.Id == coordinadorId, ct);

        if (usuario == null)
        {
            throw new ExcepcionSuperAdmin(
                404,
                "coordinador_no_encontrado",
                "Coordinador no encontrado",
                $"No existe el coordinador con ID {coordinadorId}.");
        }

        var nuevaContrasena = "Kubix" + Random.Shared.Next(100000, 999999) + "!";

        await _contexto.SaveChangesAsync(ct);

        return new RespuestaResetContrasena
        {
            Id = usuario.Id,
            Correo = usuario.Nombre,
            ContrasenaTemporal = nuevaContrasena
        };
    }

    public async Task<StatsSuperAdminDto> ObtenerStatsAsync(CancellationToken ct = default)
    {
        var universitiesCount = await _contexto.Universidades.CountAsync(ct);
        var totalUsers = await _contexto.Usuarios.CountAsync(ct);

        var driversCount = await _contexto.Usuarios.CountAsync(ct);
        var passengersCount = await _contexto.Usuarios.CountAsync(ct);

        var tripsToday = await _contexto.Viajes.CountAsync(ct);

        var activeSosCount = await _contexto.AlertasSos
            .CountAsync(s => s.Estado == EstadoAlertaSos.Activa, ct);

        return new StatsSuperAdminDto
        {
            CantidadUniversidades = universitiesCount,
            TotalUsuarios = totalUsers,
            CantidadConductores = driversCount,
            CantidadPasajeros = passengersCount,
            ViajesHoy = tripsToday,
            CantidadSosActivos = activeSosCount
        };
    }
}
