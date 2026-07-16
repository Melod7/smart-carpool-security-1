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
using Microsoft.Extensions.Logging;

namespace Kubix.Infrastructure.Usuarios;

public sealed class ServicioRegistroUsuarios(
    ContextoApp db,
    IContextoInquilino inquilino,
    IEscritorAuditoria auditoria,
    ICacheEstadoUsuario cacheEstado,
    IServicioCorreo correo,
    ILogger<ServicioRegistroUsuarios> logger) : IServicioRegistroUsuarios
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
        var genero = ValidarGeneroRequerido(solicitud.Genero);
        var imagenPerfil = ValidarImagenRequerida(
            solicitud.ImagenPerfil,
            "Profile image is required.",
            "profile_image_required");

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

        if (carrera is null)
        {
            throw ExcepcionRegistroUsuarios.Validacion(
                "Career is required.",
                "career_required");
        }

        if (cedula is null)
        {
            throw ExcepcionRegistroUsuarios.Validacion(
                "ID number is required.",
                "id_number_required");
        }

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
            Genero = genero,
            ImagenPerfil = imagenPerfil,
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
            Genero = s.Genero.HasValue
                ? ConversorEnumDominio.ACadenaDb(s.Genero.Value)
                : null,
            ImagenPerfil = s.ImagenPerfil,
            Carrera = s.Carrera,
            NumeroIdentificacion = s.NumeroIdentificacion,
            CampusId = s.CampusId,
            NombreCampus = s.Campus?.Nombre,
            VehiculoJson = s.VehiculoJson,
            Tipo = TipoSolicitud(s),
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
        var esCambioRolConductor = solicitud.HashContrasena == MarcadoresSolicitud.CambioRolConductor;
        var esCambioPerfil = solicitud.HashContrasena == MarcadoresSolicitud.CambioPerfil;
        var usuarioExistente = await db.Usuarios.FirstOrDefaultAsync(
            u => u.Correo == solicitud.Correo && u.UniversidadId == solicitud.UniversidadId,
            ct);

        if (esCambioPerfil)
        {
            if (usuarioExistente is null
                || usuarioExistente.Rol != RolUsuario.Pasajero
                || usuarioExistente.Estado == EstadoUsuario.Eliminado)
            {
                throw ExcepcionRegistroUsuarios.NoEncontrado(
                    "Passenger not found for profile change request.");
            }

            var ahoraCambio = DateTimeOffset.UtcNow;
            usuarioExistente.Nombre = solicitud.Nombre;
            usuarioExistente.Carrera = solicitud.Carrera;
            usuarioExistente.Genero = solicitud.Genero;
            usuarioExistente.ImagenPerfil = solicitud.ImagenPerfil;
            usuarioExistente.ActualizadoEn = ahoraCambio;

            solicitud.Estado = EstadoSolicitudRegistro.Aceptada;
            solicitud.DecididoPor = inquilino.UsuarioId;
            solicitud.DecididoEn = ahoraCambio;
            solicitud.ActualizadoEn = ahoraCambio;

            await RevocarTokensAsync(usuarioExistente.Id, ahoraCambio, ct);
            db.Notificaciones.Add(new Notificacion
            {
                UniversidadId = solicitud.UniversidadId,
                UsuarioDestinatarioId = usuarioExistente.Id,
                Tipo = TipoNotificacion.Auth,
                Titulo = "Cambio de perfil aprobado",
                Cuerpo = "El coordinador aprobó los cambios de tu perfil. Inicia sesión nuevamente.",
                CreadoEn = ahoraCambio
            });

            await db.SaveChangesAsync(ct);
            cacheEstado.Invalidar(usuarioExistente.Id);

            await auditoria.EscribirAsync(
                "profile.change_accepted",
                TipoEventoAuditoria.Admin,
                SeveridadAuditoria.Media,
                universidadId: solicitud.UniversidadId,
                usuarioId: usuarioExistente.Id,
                ct: ct);
            await IntentarEnviarCorreoDecisionAsync(
                solicitud.Correo,
                "Cambio de perfil aprobado",
                "Tu solicitud de cambio de perfil fue aprobada. Inicia sesión nuevamente.",
                ct);
            return;
        }

        if (esCambioRolConductor)
        {
            if (usuarioExistente is null
                || usuarioExistente.Rol != RolUsuario.Pasajero
                || usuarioExistente.Estado == EstadoUsuario.Eliminado)
            {
                throw ExcepcionRegistroUsuarios.NoEncontrado(
                    "Passenger not found for driver role change request.");
            }

            var vehiculoDto = DeserializarVehiculo(solicitud.VehiculoJson);
            var ahoraCambio = DateTimeOffset.UtcNow;
            await UpsertVehiculoAprobadoAsync(usuarioExistente, solicitud.UniversidadId, vehiculoDto, ahoraCambio, ct);

            usuarioExistente.Rol = RolUsuario.Conductor;
            usuarioExistente.ActualizadoEn = ahoraCambio;
            solicitud.Estado = EstadoSolicitudRegistro.Aceptada;
            solicitud.DecididoPor = inquilino.UsuarioId;
            solicitud.DecididoEn = ahoraCambio;
            solicitud.ActualizadoEn = ahoraCambio;

            await RevocarTokensAsync(usuarioExistente.Id, ahoraCambio, ct);
            db.Notificaciones.Add(new Notificacion
            {
                UniversidadId = solicitud.UniversidadId,
                UsuarioDestinatarioId = usuarioExistente.Id,
                Tipo = TipoNotificacion.Auth,
                Titulo = "Cambio a conductor aprobado",
                Cuerpo = "Tu solicitud fue aprobada. Inicia sesión nuevamente como conductor.",
                CreadoEn = ahoraCambio
            });

            await db.SaveChangesAsync(ct);
            cacheEstado.Invalidar(usuarioExistente.Id);

            await auditoria.EscribirAsync(
                "role.change_driver_accepted",
                TipoEventoAuditoria.Admin,
                SeveridadAuditoria.Media,
                universidadId: solicitud.UniversidadId,
                usuarioId: usuarioExistente.Id,
                ct: ct);
            await IntentarEnviarCorreoDecisionAsync(
                solicitud.Correo,
                "Cambio a conductor aprobado",
                "Tu solicitud para cambiar a conductor fue aprobada. Inicia sesión nuevamente.",
                ct);
            return;
        }

        if (esCambioVehiculo)
        {
            if (usuarioExistente is null
                || usuarioExistente.Rol != RolUsuario.Conductor
                || usuarioExistente.Estado == EstadoUsuario.Eliminado)
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
                    Imagen = vehiculoDto.Imagen,
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
                vehiculo.Imagen = vehiculoDto.Imagen;
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
            Genero = solicitud.Genero,
            ImagenPerfil = solicitud.ImagenPerfil,
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
                Imagen = vehiculoDto.Imagen,
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
        await IntentarEnviarCorreoDecisionAsync(
            solicitud.Correo,
            "Registro aprobado",
            "Tu registro en Kubix fue aprobado. Ya puedes iniciar sesión.",
            ct);
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
            solicitud.HashContrasena switch
            {
                MarcadoresSolicitud.CambioRolConductor => "role.change_driver_denied",
                MarcadoresSolicitud.CambioPerfil => "profile.change_denied",
                _ => "registration.denied"
            },
            TipoEventoAuditoria.Admin,
            SeveridadAuditoria.Media,
            universidadId: solicitud.UniversidadId,
            ct: ct);

        if (solicitud.HashContrasena != MarcadoresSolicitud.CambioVehiculo)
        {
            var esCambioRol = solicitud.HashContrasena == MarcadoresSolicitud.CambioRolConductor;
            var esCambioPerfil = solicitud.HashContrasena == MarcadoresSolicitud.CambioPerfil;
            await IntentarEnviarCorreoDecisionAsync(
                solicitud.Correo,
                esCambioRol
                    ? "Cambio a conductor denegado"
                    : esCambioPerfil
                        ? "Cambio de perfil denegado"
                        : "Registro denegado",
                esCambioRol
                    ? "Tu solicitud para cambiar a conductor fue denegada."
                    : esCambioPerfil
                        ? "Tu solicitud de cambio de perfil fue denegada."
                        : "Tu solicitud de registro en Kubix fue denegada.",
                ct);
        }
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

    public async Task EliminarUsuarioAsync(Guid usuarioId, CancellationToken ct = default)
    {
        AsegurarCoordinadorConUniversidad();
        var universidadId = inquilino.UniversidadId!.Value;

        var usuario = await db.Usuarios.FirstOrDefaultAsync(
            u => u.Id == usuarioId && u.UniversidadId == universidadId,
            ct) ?? throw ExcepcionRegistroUsuarios.NoEncontrado(
                "User not found.",
                "user_not_found");

        if (inquilino.UsuarioId == usuarioId)
        {
            throw ExcepcionRegistroUsuarios.Validacion(
                "Coordinators cannot delete themselves.",
                "cannot_delete_self");
        }

        if (usuario.Rol is RolUsuario.Coordinador or RolUsuario.SuperAdministrador)
        {
            throw ExcepcionRegistroUsuarios.Validacion(
                "Cannot delete coordinators or super admins.",
                "cannot_delete_role");
        }

        if (usuario.Estado == EstadoUsuario.Eliminado)
        {
            throw ExcepcionRegistroUsuarios.NoEncontrado(
                "User not found.",
                "user_not_found");
        }

        var conduceViajeEnCurso = await db.Viajes.AnyAsync(
            v => v.ConductorId == usuarioId && v.Estado == EstadoViaje.EnCurso,
            ct);
        var participaViajeEnCurso = await db.SolicitudesViaje.AnyAsync(
            s => s.PasajeroId == usuarioId
                 && s.Estado == EstadoSolicitudViaje.Aceptada
                 && s.Viaje.Estado == EstadoViaje.EnCurso,
            ct);

        if (conduceViajeEnCurso || participaViajeEnCurso)
        {
            throw ExcepcionRegistroUsuarios.Conflicto(
                "User participates in an in-progress trip.",
                "active_trip_blocks_user_delete");
        }

        await using var tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

        var ahora = DateTimeOffset.UtcNow;
        var viajesConducidos = await db.Viajes
            .Where(v => v.ConductorId == usuarioId && v.Estado == EstadoViaje.Programado)
            .ToListAsync(ct);
        var idsViajesConducidos = viajesConducidos.Select(v => v.Id).ToList();

        foreach (var viaje in viajesConducidos)
        {
            viaje.Estado = EstadoViaje.Cancelado;
            viaje.ActualizadoEn = ahora;
        }

        if (idsViajesConducidos.Count > 0)
        {
            var solicitudesDeViajesConducidos = await db.SolicitudesViaje
                .Where(s => idsViajesConducidos.Contains(s.ViajeId)
                            && (s.Estado == EstadoSolicitudViaje.Pendiente
                                || s.Estado == EstadoSolicitudViaje.Aceptada))
                .ToListAsync(ct);

            foreach (var solicitud in solicitudesDeViajesConducidos)
            {
                solicitud.Estado = EstadoSolicitudViaje.CanceladaPorConductor;
                solicitud.ActualizadoEn = ahora;
            }
        }

        var solicitudesComoPasajero = await db.SolicitudesViaje
            .Include(s => s.Viaje)
            .Where(s => s.PasajeroId == usuarioId
                        && !idsViajesConducidos.Contains(s.ViajeId)
                        && s.Viaje.Estado == EstadoViaje.Programado
                        && (s.Estado == EstadoSolicitudViaje.Pendiente
                            || s.Estado == EstadoSolicitudViaje.Aceptada))
            .ToListAsync(ct);

        foreach (var solicitud in solicitudesComoPasajero)
        {
            if (solicitud.Estado == EstadoSolicitudViaje.Aceptada)
            {
                solicitud.Viaje.AsientosDisponibles += 1;
                solicitud.Viaje.ActualizadoEn = ahora;
            }

            solicitud.Estado = EstadoSolicitudViaje.CanceladaPorPasajero;
            solicitud.ActualizadoEn = ahora;
        }

        var correoOriginal = usuario.Correo;
        var solicitudesRegistroUsuario = await db.SolicitudesRegistro
            .Where(s => s.UniversidadId == universidadId && s.Correo == correoOriginal)
            .ToListAsync(ct);
        foreach (var solicitud in solicitudesRegistroUsuario)
        {
            if (solicitud.Estado == EstadoSolicitudRegistro.Pendiente)
            {
                solicitud.Estado = EstadoSolicitudRegistro.Denegada;
                solicitud.DecididoPor = inquilino.UsuarioId;
                solicitud.DecididoEn = ahora;
            }

            solicitud.Nombre = "Deleted user";
            solicitud.Correo = $"deleted-{usuario.Id:D}@invalid.local";
            solicitud.Genero = null;
            solicitud.ImagenPerfil = null;
            solicitud.Carrera = null;
            solicitud.NumeroIdentificacion = null;
            solicitud.VehiculoJson = null;
            if (solicitud.HashContrasena is not (
                    MarcadoresSolicitud.CambioPerfil
                    or MarcadoresSolicitud.CambioRolConductor
                    or MarcadoresSolicitud.CambioVehiculo))
            {
                solicitud.HashContrasena = BCrypt.Net.BCrypt.HashPassword(
                    Guid.NewGuid().ToString("N"));
            }

            solicitud.ActualizadoEn = ahora;
        }

        await RevocarTokensAsync(usuarioId, ahora, ct);

        var contactos = await db.ContactosEmergencia
            .Where(c => c.UsuarioId == usuarioId)
            .ToListAsync(ct);
        db.ContactosEmergencia.RemoveRange(contactos);

        var vehiculos = await db.Vehiculos
            .Where(v => v.UsuarioId == usuarioId)
            .ToListAsync(ct);
        db.Vehiculos.RemoveRange(vehiculos);

        usuario.Estado = EstadoUsuario.Eliminado;
        usuario.Nombre = "Deleted user";
        usuario.Correo = $"deleted-{usuario.Id:D}@invalid.local";
        usuario.HashContrasena = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"));
        usuario.Genero = null;
        usuario.ImagenPerfil = null;
        usuario.Carrera = null;
        usuario.NumeroIdentificacion = null;
        usuario.DebeCambiarContrasena = false;
        usuario.ActualizadoEn = ahora;

        await db.SaveChangesAsync(ct);
        if (tx is not null)
        {
            await tx.CommitAsync(ct);
        }

        cacheEstado.Invalidar(usuarioId);
        await auditoria.EscribirAsync(
            "user.deleted",
            TipoEventoAuditoria.Admin,
            SeveridadAuditoria.Alta,
            universidadId,
            usuarioId,
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
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct)
            ?? throw ExcepcionRegistroUsuarios.NoEncontrado("User not found.");

        if (usuario.Rol == RolUsuario.Pasajero)
        {
            throw ExcepcionRegistroUsuarios.Conflicto(
                "Passenger profile changes require coordinator approval.",
                "profile_change_requires_approval");
        }

        var nombre = (solicitud.Nombre ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw ExcepcionRegistroUsuarios.Validacion("Name is required.");
        }

        usuario.Nombre = nombre;
        usuario.Carrera = NormalizarOpcional(solicitud.Carrera);
        if (solicitud.Genero is not null)
        {
            usuario.Genero = ValidarGeneroRequerido(solicitud.Genero);
        }

        if (solicitud.ImagenPerfil is not null)
        {
            usuario.ImagenPerfil = ValidarImagenRequerida(
                solicitud.ImagenPerfil,
                "Profile image is required.",
                "profile_image_required");
        }

        usuario.ActualizadoEn = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return MapearPerfil(usuario);
    }

    public async Task<RespuestaCambioPerfilDto> SolicitarCambioPerfilAsync(
        Guid usuarioId,
        SolicitudActualizarPerfil solicitud,
        CancellationToken ct = default)
    {
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct)
            ?? throw ExcepcionRegistroUsuarios.NoEncontrado("User not found.");

        if (usuario.Rol != RolUsuario.Pasajero)
        {
            throw ExcepcionRegistroUsuarios.Validacion(
                "Only passengers can request profile changes.",
                "profile_change_passenger_only");
        }

        if (usuario.UniversidadId is not Guid universidadId
            || universidadId == Guid.Empty
            || usuario.CampusId is not Guid campusId
            || campusId == Guid.Empty)
        {
            throw ExcepcionRegistroUsuarios.Validacion(
                "User university and campus are required.",
                "missing_tenant");
        }

        var nombre = (solicitud.Nombre ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw ExcepcionRegistroUsuarios.Validacion("Name is required.", "name_required");
        }

        var carrera = NormalizarOpcional(solicitud.Carrera);
        if (carrera is null)
        {
            throw ExcepcionRegistroUsuarios.Validacion(
                "Career is required.",
                "career_required");
        }

        var genero = ValidarGeneroRequerido(solicitud.Genero);
        var imagen = solicitud.ImagenPerfil is null
            ? usuario.ImagenPerfil
            : ValidarImagenRequerida(
                solicitud.ImagenPerfil,
                "Profile image is required.",
                "profile_image_required");

        var tienePendiente = await db.SolicitudesRegistro.AnyAsync(
            s => s.Correo == usuario.Correo
                 && s.UniversidadId == universidadId
                 && s.HashContrasena == MarcadoresSolicitud.CambioPerfil
                 && s.Estado == EstadoSolicitudRegistro.Pendiente,
            ct);
        if (tienePendiente)
        {
            throw ExcepcionRegistroUsuarios.Conflicto(
                "A profile change request is already pending.",
                "profile_change_pending");
        }

        var ahora = DateTimeOffset.UtcNow;
        var solicitudCambio = new SolicitudRegistro
        {
            UniversidadId = universidadId,
            CampusId = campusId,
            Nombre = nombre,
            Correo = usuario.Correo,
            HashContrasena = MarcadoresSolicitud.CambioPerfil,
            Rol = RolUsuario.Pasajero,
            Genero = genero,
            ImagenPerfil = imagen,
            Carrera = carrera,
            NumeroIdentificacion = usuario.NumeroIdentificacion,
            Estado = EstadoSolicitudRegistro.Pendiente,
            CreadoEn = ahora,
            ActualizadoEn = ahora
        };

        db.SolicitudesRegistro.Add(solicitudCambio);
        db.Notificaciones.Add(new Notificacion
        {
            UniversidadId = universidadId,
            RolDestinatario = RolUsuario.Coordinador,
            Tipo = TipoNotificacion.Auth,
            Titulo = "Cambio de perfil pendiente",
            Cuerpo = $"{usuario.Nombre} solicitó actualizar su perfil.",
            CreadoEn = ahora
        });
        await db.SaveChangesAsync(ct);

        await auditoria.EscribirAsync(
            "profile.change_requested",
            TipoEventoAuditoria.Auth,
            SeveridadAuditoria.Media,
            universidadId,
            usuario.Id,
            ct: ct);

        return new RespuestaCambioPerfilDto
        {
            Id = solicitudCambio.Id,
            Estado = "pending",
            Tipo = "profile_change",
            Mensaje = "Profile change submitted for coordinator approval."
        };
    }

    public async Task<RespuestaCambioModoDto> CambiarModoAsync(
        Guid usuarioId,
        SolicitudCambioModo solicitud,
        CancellationToken ct = default)
    {
        var modo = (solicitud.Modo ?? string.Empty).Trim().ToLowerInvariant();
        if (modo is not ("driver" or "passenger"))
        {
            throw ExcepcionRegistroUsuarios.Validacion(
                "mode must be driver or passenger.",
                "invalid_mode");
        }

        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct)
            ?? throw ExcepcionRegistroUsuarios.NoEncontrado("User not found.");

        var rolDestino = ConversorEnumDominio.DesdeCadenaDb<RolUsuario>(modo);
        if (usuario.Rol == rolDestino)
        {
            throw ExcepcionRegistroUsuarios.Conflicto(
                $"User is already in {modo} mode.",
                "already_in_mode");
        }

        if (usuario.Rol == RolUsuario.Conductor && rolDestino == RolUsuario.Pasajero)
        {
            var tieneViajeActivo = await db.Viajes.AnyAsync(
                v => v.ConductorId == usuarioId
                     && (v.Estado == EstadoViaje.Programado || v.Estado == EstadoViaje.EnCurso),
                ct);
            if (tieneViajeActivo)
            {
                throw ExcepcionRegistroUsuarios.Conflicto(
                    "Driver has a scheduled or in-progress trip.",
                    "active_trip_blocks_mode_change");
            }

            var ahora = DateTimeOffset.UtcNow;
            usuario.Rol = RolUsuario.Pasajero;
            usuario.ActualizadoEn = ahora;
            await RevocarTokensAsync(usuario.Id, ahora, ct);
            await db.SaveChangesAsync(ct);
            cacheEstado.Invalidar(usuario.Id);

            await auditoria.EscribirAsync(
                "role.changed_to_passenger",
                TipoEventoAuditoria.Auth,
                SeveridadAuditoria.Media,
                universidadId: usuario.UniversidadId,
                usuarioId: usuario.Id,
                ct: ct);

            return new RespuestaCambioModoDto
            {
                Estado = "changed",
                Tipo = "role_change_passenger",
                Rol = "passenger",
                RequiereReautenticacion = true,
                Mensaje = "Mode changed to passenger. Sign in again."
            };
        }

        if (usuario.Rol != RolUsuario.Pasajero || rolDestino != RolUsuario.Conductor)
        {
            throw ExcepcionRegistroUsuarios.Validacion(
                "Only driver/passenger mode changes are supported.",
                "invalid_mode_change");
        }

        if (usuario.UniversidadId is not Guid universidadId || usuario.CampusId is not Guid campusId)
        {
            throw ExcepcionRegistroUsuarios.Validacion(
                "User university and campus are required.",
                "missing_tenant");
        }

        var vehiculoJson = SerializarVehiculo(solicitud.Vehiculo);
        if (await db.SolicitudesRegistro.AnyAsync(
                s => s.Correo == usuario.Correo
                     && s.UniversidadId == universidadId
                     && s.Estado == EstadoSolicitudRegistro.Pendiente
                     && s.HashContrasena == MarcadoresSolicitud.CambioRolConductor,
                ct))
        {
            throw ExcepcionRegistroUsuarios.Conflicto(
                "A driver role change request is already pending.",
                "role_change_driver_pending");
        }

        var ahoraSolicitud = DateTimeOffset.UtcNow;
        var solicitudCambio = new SolicitudRegistro
        {
            UniversidadId = universidadId,
            CampusId = campusId,
            Nombre = usuario.Nombre,
            Correo = usuario.Correo,
            HashContrasena = MarcadoresSolicitud.CambioRolConductor,
            Rol = RolUsuario.Conductor,
            Genero = usuario.Genero,
            ImagenPerfil = usuario.ImagenPerfil,
            Carrera = usuario.Carrera,
            NumeroIdentificacion = usuario.NumeroIdentificacion,
            VehiculoJson = vehiculoJson,
            Estado = EstadoSolicitudRegistro.Pendiente,
            CreadoEn = ahoraSolicitud,
            ActualizadoEn = ahoraSolicitud
        };
        db.SolicitudesRegistro.Add(solicitudCambio);
        db.Notificaciones.Add(new Notificacion
        {
            UniversidadId = universidadId,
            RolDestinatario = RolUsuario.Coordinador,
            Tipo = TipoNotificacion.Auth,
            Titulo = "Cambio a conductor pendiente",
            Cuerpo = $"{usuario.Nombre} solicitó cambiar su rol a conductor.",
            CreadoEn = ahoraSolicitud
        });
        await db.SaveChangesAsync(ct);

        await auditoria.EscribirAsync(
            "role.change_driver_requested",
            TipoEventoAuditoria.Auth,
            SeveridadAuditoria.Media,
            universidadId,
            usuario.Id,
            ct: ct);

        return new RespuestaCambioModoDto
        {
            Estado = "pending",
            Tipo = "role_change_driver",
            Rol = "passenger",
            SolicitudId = solicitudCambio.Id,
            RequiereReautenticacion = false,
            Mensaje = "Driver role change submitted for coordinator approval."
        };
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
            .Where(u => u.Rol != RolUsuario.SuperAdministrador
                        && u.Estado != EstadoUsuario.Eliminado);

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
        if (inquilino.Rol != RolUsuario.Coordinador)
        {
            throw ExcepcionRegistroUsuarios.Validacion(
                "Coordinator context is required.",
                "coordinator_required");
        }

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
            seatsTotal = vehiculo.AsientosTotales,
            image = vehiculo.Imagen!.Trim()
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

        if (string.IsNullOrWhiteSpace(vehiculo.Imagen))
        {
            throw ExcepcionRegistroUsuarios.Validacion(
                "Vehicle image is required.",
                "vehicle_image_required");
        }

        if (!ValidadorImagenDataUrl.EsValida(vehiculo.Imagen))
        {
            throw ExcepcionRegistroUsuarios.Validacion(
                "Vehicle image must be a JPEG, PNG or WebP data URL of at most 2 MiB.",
                "invalid_vehicle_image");
        }
    }

    private static GeneroUsuario ValidarGeneroRequerido(string? genero)
    {
        var valor = (genero ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw ExcepcionRegistroUsuarios.Validacion(
                "Gender is required.",
                "gender_required");
        }

        if (valor is not ("male" or "female"))
        {
            throw ExcepcionRegistroUsuarios.Validacion(
                "Gender must be male or female.",
                "invalid_gender");
        }

        return ConversorEnumDominio.DesdeCadenaDb<GeneroUsuario>(valor);
    }

    private static string ValidarImagenRequerida(
        string? imagen,
        string mensajeRequerida,
        string codigoRequerida)
    {
        if (string.IsNullOrWhiteSpace(imagen))
        {
            throw ExcepcionRegistroUsuarios.Validacion(mensajeRequerida, codigoRequerida);
        }

        var valor = imagen.Trim();
        if (!ValidadorImagenDataUrl.EsValida(valor))
        {
            throw ExcepcionRegistroUsuarios.Validacion(
                "Image must be a JPEG, PNG or WebP data URL of at most 2 MiB.",
                "invalid_image");
        }

        return valor;
    }

    private async Task RevocarTokensAsync(Guid usuarioId, DateTimeOffset ahora, CancellationToken ct)
    {
        var tokens = await db.TokensRefresco
            .Where(t => t.UsuarioId == usuarioId && t.RevocadoEn == null)
            .ToListAsync(ct);
        foreach (var token in tokens)
        {
            token.RevocadoEn = ahora;
        }
    }

    private async Task IntentarEnviarCorreoDecisionAsync(
        string destinatario,
        string asunto,
        string cuerpo,
        CancellationToken ct)
    {
        try
        {
            await correo.EnviarAsync(destinatario, asunto, cuerpo, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "No se pudo enviar correo de decisión a {Destinatario}; la decisión ya fue persistida",
                destinatario);
        }
    }

    private async Task UpsertVehiculoAprobadoAsync(
        Usuario usuario,
        Guid universidadId,
        VehiculoRegistroDto dto,
        DateTimeOffset ahora,
        CancellationToken ct)
    {
        var vehiculo = await db.Vehiculos.FirstOrDefaultAsync(v => v.UsuarioId == usuario.Id, ct);
        if (vehiculo is null)
        {
            db.Vehiculos.Add(new Vehiculo
            {
                UniversidadId = universidadId,
                UsuarioId = usuario.Id,
                MarcaModelo = dto.MarcaModelo.Trim(),
                Placa = dto.Placa.Trim(),
                Color = dto.Color.Trim(),
                AsientosTotales = dto.AsientosTotales,
                Imagen = dto.Imagen!.Trim(),
                CreadoEn = ahora,
                ActualizadoEn = ahora
            });
            return;
        }

        vehiculo.MarcaModelo = dto.MarcaModelo.Trim();
        vehiculo.Placa = dto.Placa.Trim();
        vehiculo.Color = dto.Color.Trim();
        vehiculo.AsientosTotales = dto.AsientosTotales;
        vehiculo.Imagen = dto.Imagen!.Trim();
        vehiculo.ActualizadoEn = ahora;
    }

    private static string TipoSolicitud(SolicitudRegistro solicitud) =>
        solicitud.HashContrasena switch
        {
            MarcadoresSolicitud.CambioVehiculo => "vehicle_change",
            MarcadoresSolicitud.CambioRolConductor => "role_change_driver",
            MarcadoresSolicitud.CambioPerfil => "profile_change",
            _ => "registration"
        };

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
        Genero = u.Genero.HasValue ? ConversorEnumDominio.ACadenaDb(u.Genero.Value) : null,
        ImagenPerfil = u.ImagenPerfil,
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
        Genero = u.Genero.HasValue ? ConversorEnumDominio.ACadenaDb(u.Genero.Value) : null,
        ImagenPerfil = u.ImagenPerfil,
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
