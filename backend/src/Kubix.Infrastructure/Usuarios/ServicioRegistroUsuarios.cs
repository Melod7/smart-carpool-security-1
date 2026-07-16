using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kubix.Application.Auth;
using Kubix.Application.Tenancy;
using Kubix.Application.Usuarios;
using Kubix.Domain;
using Kubix.Domain.Entities;
using Kubix.Domain.Enums;
using Kubix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kubix.Infrastructure.Usuarios;

public sealed class ServicioRegistroUsuarios(
    ContextoApp db,
    IContextoInquilino inquilino,
    IEscritorAuditoria auditoria,
    ICacheEstadoUsuario cacheEstado) : IServicioRegistroUsuarios
{
    private static readonly JsonSerializerOptions JsonOpciones = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Regex TelefonoValido = new(
        @"^\+?[0-9]{7,15}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<RespuestaRegistroDto> RegistrarAsync(
        SolicitudRegistroDto solicitud,
        CancellationToken ct = default)
    {
        var nombre = (solicitud.Nombre ?? string.Empty).Trim();
        var correo = (solicitud.Correo ?? string.Empty).Trim().ToLowerInvariant();
        var contrasena = solicitud.Contrasena ?? string.Empty;
        var carrera = NormalizarOpcional(solicitud.Carrera);
        var cedula = NormalizarOpcional(solicitud.NumeroIdentificacion);
        var rolTexto = (solicitud.Rol ?? string.Empty).Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw ExcepcionRegistroUsuarios.Validacion("Name is required.");
        }

        if (string.IsNullOrWhiteSpace(correo) || !correo.Contains('@'))
        {
            throw ExcepcionRegistroUsuarios.Validacion("A valid email is required.");
        }

        if (contrasena.Length < 8)
        {
            throw ExcepcionRegistroUsuarios.Validacion("Password must be at least 8 characters.");
        }

        if (rolTexto is not ("driver" or "passenger"))
        {
            throw ExcepcionRegistroUsuarios.Validacion(
                "Role must be driver or passenger.",
                "invalid_role");
        }

        var rol = ConversorEnumDominio.DesdeCadenaDb<RolUsuario>(rolTexto);

        var universidad = await db.Universidades
            .Include(u => u.Configuracion)
            .FirstOrDefaultAsync(u => u.Id == solicitud.UniversidadId, ct)
            ?? throw ExcepcionRegistroUsuarios.Validacion(
                "University not found.",
                "university_not_found");

        if (universidad.Estado != EstadoUniversidad.Activa)
        {
            throw ExcepcionRegistroUsuarios.Validacion(
                "University is not accepting registrations.",
                "university_inactive");
        }

        var campus = await db.Sedes.FirstOrDefaultAsync(
            c => c.Id == solicitud.CampusId && c.UniversidadId == universidad.Id,
            ct)
            ?? throw ExcepcionRegistroUsuarios.Validacion(
                "Campus not found for the selected university.",
                "campus_not_found");

        ValidarDominioCorreo(correo, universidad.Configuracion?.DominioCorreoPermitido);

        if (await db.Usuarios.AnyAsync(
                u => u.Correo == correo && u.UniversidadId == universidad.Id,
                ct))
        {
            throw ExcepcionRegistroUsuarios.Conflicto("Email is already registered.", "email_taken");
        }

        if (await db.SolicitudesRegistro.AnyAsync(
                s => s.Correo == correo
                     && s.UniversidadId == universidad.Id
                     && s.Estado == EstadoSolicitudRegistro.Pendiente,
                ct))
        {
            throw ExcepcionRegistroUsuarios.Conflicto(
                "A pending registration already exists for this email.",
                "registration_pending");
        }

        string? vehiculoJson = null;
        if (rol == RolUsuario.Conductor)
        {
            vehiculoJson = SerializarVehiculo(solicitud.Vehiculo);
        }
        else if (solicitud.Vehiculo is not null)
        {
            throw ExcepcionRegistroUsuarios.Validacion(
                "Vehicle is only allowed for driver registrations.",
                "vehicle_not_allowed");
        }

        var ahora = DateTimeOffset.UtcNow;
        var registro = new SolicitudRegistro
        {
            UniversidadId = universidad.Id,
            CampusId = campus.Id,
            Nombre = nombre,
            Correo = correo,
            HashContrasena = BCrypt.Net.BCrypt.HashPassword(contrasena),
            Rol = rol,
            Carrera = carrera,
            NumeroIdentificacion = cedula,
            VehiculoJson = vehiculoJson,
            Estado = EstadoSolicitudRegistro.Pendiente,
            CreadoEn = ahora,
            ActualizadoEn = ahora
        };

        db.SolicitudesRegistro.Add(registro);
        await db.SaveChangesAsync(ct);

        await auditoria.EscribirAsync(
            "registration.submitted",
            TipoEventoAuditoria.Auth,
            SeveridadAuditoria.Baja,
            universidadId: universidad.Id,
            ct: ct);

        return new RespuestaRegistroDto
        {
            Id = registro.Id,
            Estado = ConversorEnumDominio.ACadenaDb(registro.Estado),
            Correo = registro.Correo
        };
    }

    public async Task<IReadOnlyList<UniversidadPublicaDto>> ListarUniversidadesPublicasAsync(
        CancellationToken ct = default)
    {
        var filas = await db.Universidades
            .AsNoTracking()
            .Where(u => u.Estado == EstadoUniversidad.Activa)
            .OrderBy(u => u.Nombre)
            .Select(u => new
            {
                u.Id,
                u.Nombre,
                Campuses = u.Sedes
                    .OrderBy(c => c.Nombre)
                    .Select(c => new CampusPublicoDto { Id = c.Id, Nombre = c.Nombre })
                    .ToList()
            })
            .ToListAsync(ct);

        return filas.Select(u => new UniversidadPublicaDto
        {
            Id = u.Id,
            Nombre = u.Nombre,
            Campuses = u.Campuses
        }).ToList();
    }

    public async Task<IReadOnlyList<SolicitudRegistroResumenDto>> ListarSolicitudesPendientesAsync(
        CancellationToken ct = default)
    {
        AsegurarCoordinadorConUniversidad();

        var filas = await db.SolicitudesRegistro
            .AsNoTracking()
            .Include(s => s.Campus)
            .Where(s => s.Estado == EstadoSolicitudRegistro.Pendiente)
            .OrderBy(s => s.CreadoEn)
            .ToListAsync(ct);

        return filas.Select(s => new SolicitudRegistroResumenDto
        {
            Id = s.Id,
            Nombre = s.Nombre,
            Correo = s.Correo,
            Rol = ConversorEnumDominio.ACadenaDb(s.Rol),
            Carrera = s.Carrera,
            NumeroIdentificacion = s.NumeroIdentificacion,
            CampusId = s.CampusId,
            NombreCampus = s.Campus?.Nombre,
            VehiculoJson = s.VehiculoJson,
            Tipo = s.HashContrasena == MarcadoresSolicitud.CambioVehiculo
                ? "vehicle_change"
                : "registration",
            Estado = ConversorEnumDominio.ACadenaDb(s.Estado),
            CreadoEn = s.CreadoEn
        }).ToList();
    }

    public async Task AceptarSolicitudAsync(Guid solicitudId, CancellationToken ct = default)
    {
        AsegurarCoordinadorConUniversidad();

        var solicitud = await db.SolicitudesRegistro.FirstOrDefaultAsync(s => s.Id == solicitudId, ct)
            ?? throw ExcepcionRegistroUsuarios.NoEncontrado("Registration request not found.");

        if (solicitud.Estado != EstadoSolicitudRegistro.Pendiente)
        {
            throw ExcepcionRegistroUsuarios.Conflicto(
                "Registration request is not pending.",
                "request_not_pending");
        }

        var esCambioVehiculo = solicitud.HashContrasena == MarcadoresSolicitud.CambioVehiculo;
        var usuarioExistente = await db.Usuarios.FirstOrDefaultAsync(
            u => u.Correo == solicitud.Correo && u.UniversidadId == solicitud.UniversidadId,
            ct);

        if (esCambioVehiculo)
        {
            if (usuarioExistente is null || usuarioExistente.Rol != RolUsuario.Conductor)
            {
                throw ExcepcionRegistroUsuarios.NoEncontrado(
                    "Driver not found for vehicle change request.");
            }

            var vehiculoDto = DeserializarVehiculo(solicitud.VehiculoJson);
            var ahoraCambio = DateTimeOffset.UtcNow;
            var vehiculo = await db.Vehiculos.FirstOrDefaultAsync(
                v => v.UsuarioId == usuarioExistente.Id,
                ct);

            if (vehiculo is null)
            {
                db.Vehiculos.Add(new Vehiculo
                {
                    UniversidadId = solicitud.UniversidadId,
                    UsuarioId = usuarioExistente.Id,
                    MarcaModelo = vehiculoDto.MarcaModelo,
                    Placa = vehiculoDto.Placa,
                    Color = vehiculoDto.Color,
                    AsientosTotales = vehiculoDto.AsientosTotales,
                    CreadoEn = ahoraCambio,
                    ActualizadoEn = ahoraCambio
                });
            }
            else
            {
                vehiculo.MarcaModelo = vehiculoDto.MarcaModelo;
                vehiculo.Placa = vehiculoDto.Placa;
                vehiculo.Color = vehiculoDto.Color;
                vehiculo.AsientosTotales = vehiculoDto.AsientosTotales;
                vehiculo.ActualizadoEn = ahoraCambio;
            }

            solicitud.Estado = EstadoSolicitudRegistro.Aceptada;
            solicitud.DecididoPor = inquilino.UsuarioId;
            solicitud.DecididoEn = ahoraCambio;
            solicitud.ActualizadoEn = ahoraCambio;

            db.Notificaciones.Add(new Notificacion
            {
                UniversidadId = solicitud.UniversidadId,
                UsuarioDestinatarioId = usuarioExistente.Id,
                Tipo = TipoNotificacion.Auth,
                Titulo = "Cambio de vehículo aprobado",
                Cuerpo = "El coordinador aprobó los nuevos datos de tu vehículo.",
                CreadoEn = ahoraCambio
            });

            await db.SaveChangesAsync(ct);

            await auditoria.EscribirAsync(
                "vehicle.change_accepted",
                TipoEventoAuditoria.Admin,
                SeveridadAuditoria.Media,
                universidadId: solicitud.UniversidadId,
                usuarioId: usuarioExistente.Id,
                ct: ct);
            return;
        }

        if (usuarioExistente is not null)
        {
            throw ExcepcionRegistroUsuarios.Conflicto("Email is already registered.", "email_taken");
        }

        var ahora = DateTimeOffset.UtcNow;
        var usuario = new Usuario
        {
            UniversidadId = solicitud.UniversidadId,
            CampusId = solicitud.CampusId,
            Rol = solicitud.Rol,
            Estado = EstadoUsuario.Activo,
            Nombre = solicitud.Nombre,
            Correo = solicitud.Correo,
            HashContrasena = solicitud.HashContrasena,
            Carrera = solicitud.Carrera,
            NumeroIdentificacion = solicitud.NumeroIdentificacion,
            CreadoEn = ahora,
            ActualizadoEn = ahora
        };

        db.Usuarios.Add(usuario);

        if (solicitud.Rol == RolUsuario.Conductor)
        {
            var vehiculoDto = DeserializarVehiculo(solicitud.VehiculoJson);
            db.Vehiculos.Add(new Vehiculo
            {
                UniversidadId = solicitud.UniversidadId,
                UsuarioId = usuario.Id,
                MarcaModelo = vehiculoDto.MarcaModelo,
                Placa = vehiculoDto.Placa,
                Color = vehiculoDto.Color,
                AsientosTotales = vehiculoDto.AsientosTotales,
                CreadoEn = ahora,
                ActualizadoEn = ahora
            });
        }

        solicitud.Estado = EstadoSolicitudRegistro.Aceptada;
        solicitud.DecididoPor = inquilino.UsuarioId;
        solicitud.DecididoEn = ahora;
        solicitud.ActualizadoEn = ahora;

        db.Notificaciones.Add(new Notificacion
        {
            UniversidadId = solicitud.UniversidadId,
            RolDestinatario = RolUsuario.Coordinador,
            Tipo = TipoNotificacion.Auth,
            Titulo = "Registro aceptado",
            Cuerpo = $"{usuario.Nombre} ({usuario.Correo}) fue aceptado.",
            CreadoEn = ahora
        });

        await db.SaveChangesAsync(ct);

        await auditoria.EscribirAsync(
            "registration.accepted",
            TipoEventoAuditoria.Admin,
            SeveridadAuditoria.Media,
            universidadId: solicitud.UniversidadId,
            usuarioId: usuario.Id,
            ct: ct);
    }

    public async Task DenegarSolicitudAsync(Guid solicitudId, CancellationToken ct = default)
    {
        AsegurarCoordinadorConUniversidad();

        var solicitud = await db.SolicitudesRegistro.FirstOrDefaultAsync(s => s.Id == solicitudId, ct)
            ?? throw ExcepcionRegistroUsuarios.NoEncontrado("Registration request not found.");

        if (solicitud.Estado != EstadoSolicitudRegistro.Pendiente)
        {
            throw ExcepcionRegistroUsuarios.Conflicto(
                "Registration request is not pending.",
                "request_not_pending");
        }

        var ahora = DateTimeOffset.UtcNow;
        solicitud.Estado = EstadoSolicitudRegistro.Denegada;
        solicitud.DecididoPor = inquilino.UsuarioId;
        solicitud.DecididoEn = ahora;
        solicitud.ActualizadoEn = ahora;
        await db.SaveChangesAsync(ct);

        await auditoria.EscribirAsync(
            "registration.denied",
            TipoEventoAuditoria.Admin,
            SeveridadAuditoria.Media,
            universidadId: solicitud.UniversidadId,
            ct: ct);
    }

    public async Task<PaginaUsuariosAdmin> ListarUsuariosAsync(
        FiltroUsuariosAdmin filtro,
        CancellationToken ct = default)
    {
        AsegurarCoordinadorConUniversidad();
        var (pagina, tamano) = NormalizarPaginacion(filtro.Pagina, filtro.TamanoPagina);
        var query = ConstruirConsultaUsuarios(filtro);

        var total = await query.CountAsync(ct);
        var filas = await query
            .OrderBy(u => u.Nombre)
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .ToListAsync(ct);

        return new PaginaUsuariosAdmin
        {
            Total = total,
            Items = filas.Select(MapearUsuarioAdmin).ToList()
        };
    }

    public async Task BloquearUsuarioAsync(Guid usuarioId, CancellationToken ct = default)
    {
        AsegurarCoordinadorConUniversidad();

        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct)
            ?? throw ExcepcionRegistroUsuarios.NoEncontrado("User not found.");

        if (usuario.Rol is RolUsuario.Coordinador or RolUsuario.SuperAdministrador)
        {
            throw ExcepcionRegistroUsuarios.Validacion(
                "Cannot block coordinators or super admins.",
                "cannot_block_role");
        }

        if (usuario.Estado == EstadoUsuario.Bloqueado)
        {
            return;
        }

        usuario.Estado = EstadoUsuario.Bloqueado;
        usuario.ActualizadoEn = DateTimeOffset.UtcNow;

        var ahora = DateTimeOffset.UtcNow;
        var tokens = await db.TokensRefresco
            .Where(t => t.UsuarioId == usuarioId && t.RevocadoEn == null)
            .ToListAsync(ct);
        foreach (var token in tokens)
        {
            token.RevocadoEn = ahora;
        }

        await db.SaveChangesAsync(ct);
        cacheEstado.Invalidar(usuarioId);

        await auditoria.EscribirAsync(
            "user.blocked",
            TipoEventoAuditoria.Admin,
            SeveridadAuditoria.Alta,
            universidadId: usuario.UniversidadId,
            usuarioId: usuario.Id,
            ct: ct);
    }

    public async Task DesbloquearUsuarioAsync(Guid usuarioId, CancellationToken ct = default)
    {
        AsegurarCoordinadorConUniversidad();

        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct)
            ?? throw ExcepcionRegistroUsuarios.NoEncontrado("User not found.");

        if (usuario.Estado != EstadoUsuario.Bloqueado)
        {
            throw ExcepcionRegistroUsuarios.Conflicto(
                "User is not blocked.",
                "user_not_blocked");
        }

        usuario.Estado = EstadoUsuario.Activo;
        usuario.ActualizadoEn = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        cacheEstado.Invalidar(usuarioId);

        await auditoria.EscribirAsync(
            "user.unblocked",
            TipoEventoAuditoria.Admin,
            SeveridadAuditoria.Media,
            universidadId: usuario.UniversidadId,
            usuarioId: usuario.Id,
            ct: ct);
    }

    public async Task<string> ExportarUsuariosCsvAsync(
        FiltroUsuariosAdmin filtro,
        CancellationToken ct = default)
    {
        AsegurarCoordinadorConUniversidad();
        var query = ConstruirConsultaUsuarios(filtro);
        var filas = await query.OrderBy(u => u.Nombre).ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("id,name,email,role,status,campusId,career,idNumber,ratingAvg,createdAt");
        foreach (var u in filas)
        {
            sb.Append(Csv(u.Id.ToString()))
                .Append(',')
                .Append(Csv(u.Nombre))
                .Append(',')
                .Append(Csv(u.Correo))
                .Append(',')
                .Append(Csv(ConversorEnumDominio.ACadenaDb(u.Rol)))
                .Append(',')
                .Append(Csv(ConversorEnumDominio.ACadenaDb(u.Estado)))
                .Append(',')
                .Append(Csv(u.CampusId?.ToString() ?? string.Empty))
                .Append(',')
                .Append(Csv(u.Carrera ?? string.Empty))
                .Append(',')
                .Append(Csv(u.NumeroIdentificacion ?? string.Empty))
                .Append(',')
                .Append(Csv(u.PromedioCalificacion.ToString(CultureInfo.InvariantCulture)))
                .Append(',')
                .Append(Csv(u.CreadoEn.ToString("O", CultureInfo.InvariantCulture)))
                .AppendLine();
        }

        return sb.ToString();
    }

    public async Task<PerfilUsuarioDto> ObtenerPerfilAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var usuario = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == usuarioId, ct)
            ?? throw ExcepcionRegistroUsuarios.NoEncontrado("User not found.");

        return MapearPerfil(usuario);
    }

    public async Task<PerfilUsuarioDto> ActualizarPerfilAsync(
        Guid usuarioId,
        SolicitudActualizarPerfil solicitud,
        CancellationToken ct = default)
    {
        var nombre = (solicitud.Nombre ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw ExcepcionRegistroUsuarios.Validacion("Name is required.");
        }

        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct)
            ?? throw ExcepcionRegistroUsuarios.NoEncontrado("User not found.");

        usuario.Nombre = nombre;
        usuario.Carrera = NormalizarOpcional(solicitud.Carrera);
        usuario.ActualizadoEn = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return MapearPerfil(usuario);
    }

    public async Task<IReadOnlyList<ContactoEmergenciaDto>> ListarContactosEmergenciaAsync(
        Guid usuarioId,
        CancellationToken ct = default)
    {
        await AsegurarUsuarioExisteAsync(usuarioId, ct);

        var contactos = await db.ContactosEmergencia
            .AsNoTracking()
            .Where(c => c.UsuarioId == usuarioId)
            .OrderBy(c => c.CreadoEn)
            .ToListAsync(ct);

        return contactos.Select(MapearContacto).ToList();
    }

    public async Task<ContactoEmergenciaDto> CrearContactoEmergenciaAsync(
        Guid usuarioId,
        SolicitudCrearContactoEmergencia solicitud,
        CancellationToken ct = default)
    {
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct)
            ?? throw ExcepcionRegistroUsuarios.NoEncontrado("User not found.");

        if (usuario.UniversidadId is null || usuario.UniversidadId == Guid.Empty)
        {
            throw ExcepcionRegistroUsuarios.Validacion(
                "User does not belong to a university.",
                "missing_university");
        }

        var nombre = (solicitud.Nombre ?? string.Empty).Trim();
        var relacion = (solicitud.Relacion ?? string.Empty).Trim();
        var telefono = (solicitud.Telefono ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw ExcepcionRegistroUsuarios.Validacion("Name is required.");
        }

        if (string.IsNullOrWhiteSpace(relacion))
        {
            throw ExcepcionRegistroUsuarios.Validacion("Relationship is required.");
        }

        if (!TelefonoValido.IsMatch(telefono))
        {
            throw ExcepcionRegistroUsuarios.Validacion(
                "Phone must be 7–15 digits, optionally prefixed with +.",
                "invalid_phone");
        }

        var cantidad = await db.ContactosEmergencia.CountAsync(c => c.UsuarioId == usuarioId, ct);
        if (cantidad >= 3)
        {
            throw ExcepcionRegistroUsuarios.Validacion(
                "Maximum of 3 emergency contacts allowed.",
                "max_emergency_contacts");
        }

        var ahora = DateTimeOffset.UtcNow;
        var contacto = new ContactoEmergencia
        {
            UniversidadId = usuario.UniversidadId.Value,
            UsuarioId = usuarioId,
            Nombre = nombre,
            Relacion = relacion,
            Telefono = telefono,
            CreadoEn = ahora,
            ActualizadoEn = ahora
        };

        db.ContactosEmergencia.Add(contacto);
        await db.SaveChangesAsync(ct);
        return MapearContacto(contacto);
    }

    public async Task EliminarContactoEmergenciaAsync(
        Guid usuarioId,
        Guid contactoId,
        CancellationToken ct = default)
    {
        var contacto = await db.ContactosEmergencia.FirstOrDefaultAsync(
            c => c.Id == contactoId && c.UsuarioId == usuarioId,
            ct) ?? throw ExcepcionRegistroUsuarios.NoEncontrado("Emergency contact not found.");

        db.ContactosEmergencia.Remove(contacto);
        await db.SaveChangesAsync(ct);
    }

    private IQueryable<Usuario> ConstruirConsultaUsuarios(FiltroUsuariosAdmin filtro)
    {
        var query = db.Usuarios.AsNoTracking()
            .Where(u => u.Rol != RolUsuario.SuperAdministrador);

        if (!string.IsNullOrWhiteSpace(filtro.Estado))
        {
            EstadoUsuario estado;
            try
            {
                estado = ConversorEnumDominio.DesdeCadenaDb<EstadoUsuario>(
                    filtro.Estado.Trim().ToLowerInvariant());
            }
            catch (ArgumentOutOfRangeException)
            {
                throw ExcepcionRegistroUsuarios.Validacion("Invalid status filter.", "invalid_status");
            }

            query = query.Where(u => u.Estado == estado);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Rol))
        {
            RolUsuario rol;
            try
            {
                rol = ConversorEnumDominio.DesdeCadenaDb<RolUsuario>(
                    filtro.Rol.Trim().ToLowerInvariant());
            }
            catch (ArgumentOutOfRangeException)
            {
                throw ExcepcionRegistroUsuarios.Validacion("Invalid role filter.", "invalid_role");
            }

            query = query.Where(u => u.Rol == rol);
        }

        if (filtro.CampusId is Guid campusId && campusId != Guid.Empty)
        {
            query = query.Where(u => u.CampusId == campusId);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Busqueda))
        {
            var texto = filtro.Busqueda.Trim().ToLowerInvariant();
            query = query.Where(u =>
                u.Nombre.ToLower().Contains(texto)
                || u.Correo.ToLower().Contains(texto)
                || (u.NumeroIdentificacion != null && u.NumeroIdentificacion.ToLower().Contains(texto)));
        }

        return query;
    }

    private void AsegurarCoordinadorConUniversidad()
    {
        if (inquilino.UniversidadId is null || inquilino.UniversidadId == Guid.Empty)
        {
            throw ExcepcionRegistroUsuarios.Validacion(
                "Coordinator university context is required.",
                "missing_tenant");
        }
    }

    private async Task AsegurarUsuarioExisteAsync(Guid usuarioId, CancellationToken ct)
    {
        if (!await db.Usuarios.AnyAsync(u => u.Id == usuarioId, ct))
        {
            throw ExcepcionRegistroUsuarios.NoEncontrado("User not found.");
        }
    }

    private static void ValidarDominioCorreo(string correo, string? dominioPermitido)
    {
        if (string.IsNullOrWhiteSpace(dominioPermitido))
        {
            return;
        }

        var dominio = dominioPermitido.Trim().ToLowerInvariant();
        var parteDominio = correo.Split('@')[^1];
        if (!string.Equals(parteDominio, dominio, StringComparison.Ordinal))
        {
            throw ExcepcionRegistroUsuarios.Validacion(
                $"Email domain must be @{dominio}.",
                "invalid_email_domain");
        }
    }

    private static string SerializarVehiculo(VehiculoRegistroDto? vehiculo)
    {
        if (vehiculo is null)
        {
            throw ExcepcionRegistroUsuarios.Validacion(
                "Vehicle is required for driver registrations.",
                "vehicle_required");
        }

        ValidarVehiculo(vehiculo);
        return JsonSerializer.Serialize(new
        {
            makeModel = vehiculo.MarcaModelo.Trim(),
            plate = vehiculo.Placa.Trim(),
            color = vehiculo.Color.Trim(),
            seatsTotal = vehiculo.AsientosTotales
        });
    }

    private static VehiculoRegistroDto DeserializarVehiculo(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw ExcepcionRegistroUsuarios.Validacion(
                "Driver registration is missing vehicle data.",
                "vehicle_required");
        }

        VehiculoRegistroDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<VehiculoRegistroDto>(json, JsonOpciones);
        }
        catch (JsonException)
        {
            throw ExcepcionRegistroUsuarios.Validacion(
                "Invalid vehicle JSON.",
                "invalid_vehicle_json");
        }

        if (dto is null)
        {
            throw ExcepcionRegistroUsuarios.Validacion(
                "Invalid vehicle JSON.",
                "invalid_vehicle_json");
        }

        ValidarVehiculo(dto);
        return dto;
    }

    private static void ValidarVehiculo(VehiculoRegistroDto vehiculo)
    {
        if (string.IsNullOrWhiteSpace(vehiculo.MarcaModelo)
            || string.IsNullOrWhiteSpace(vehiculo.Placa)
            || string.IsNullOrWhiteSpace(vehiculo.Color))
        {
            throw ExcepcionRegistroUsuarios.Validacion(
                "Vehicle makeModel, plate and color are required.",
                "invalid_vehicle");
        }

        if (vehiculo.AsientosTotales is < 1 or > 8)
        {
            throw ExcepcionRegistroUsuarios.Validacion(
                "Vehicle seatsTotal must be between 1 and 8.",
                "invalid_vehicle_seats");
        }
    }

    private static (int pagina, int tamano) NormalizarPaginacion(int pagina, int tamano)
    {
        if (pagina < 1)
        {
            pagina = 1;
        }

        if (tamano < 1)
        {
            tamano = 20;
        }

        if (tamano > 100)
        {
            tamano = 100;
        }

        return (pagina, tamano);
    }

    private static string? NormalizarOpcional(string? valor)
    {
        var t = (valor ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(t) ? null : t;
    }

    private static string Csv(string valor)
    {
        if (valor.Contains('"') || valor.Contains(',') || valor.Contains('\n') || valor.Contains('\r'))
        {
            return $"\"{valor.Replace("\"", "\"\"")}\"";
        }

        return valor;
    }

    private static UsuarioAdminDto MapearUsuarioAdmin(Usuario u) => new()
    {
        Id = u.Id,
        Nombre = u.Nombre,
        Correo = u.Correo,
        Rol = ConversorEnumDominio.ACadenaDb(u.Rol),
        Estado = ConversorEnumDominio.ACadenaDb(u.Estado),
        CampusId = u.CampusId,
        Carrera = u.Carrera,
        NumeroIdentificacion = u.NumeroIdentificacion,
        PromedioCalificacion = u.PromedioCalificacion,
        CreadoEn = u.CreadoEn
    };

    private static PerfilUsuarioDto MapearPerfil(Usuario u) => new()
    {
        Id = u.Id,
        Nombre = u.Nombre,
        Correo = u.Correo,
        Rol = ConversorEnumDominio.ACadenaDb(u.Rol),
        Carrera = u.Carrera,
        CampusId = u.CampusId,
        UniversidadId = u.UniversidadId,
        Estado = ConversorEnumDominio.ACadenaDb(u.Estado)
    };

    private static ContactoEmergenciaDto MapearContacto(ContactoEmergencia c) => new()
    {
        Id = c.Id,
        Nombre = c.Nombre,
        Relacion = c.Relacion,
        Telefono = c.Telefono
    };
}
