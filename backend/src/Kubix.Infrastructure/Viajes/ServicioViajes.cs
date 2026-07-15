using Kubix.Application.EcoTokens;
using Kubix.Application.Tenancy;
using Kubix.Application.Viajes;
using Kubix.Domain;
using Kubix.Domain.Entities;
using Kubix.Domain.Enums;
using Kubix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kubix.Infrastructure.Viajes;

public sealed class ServicioViajes(
    ContextoApp db,
    IServicioDirections directions,
    IEscritorAuditoria auditoria,
    IMotorEcoTokens motorEcoTokens) : IServicioViajes
{
    private static readonly TimeSpan VentanaCancelacionTardia = TimeSpan.FromMinutes(30);

    public async Task<VehiculoDto> ObtenerVehiculoAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var usuario = await ObtenerUsuarioAsync(usuarioId, ct);
        AsegurarConductor(usuario);

        var vehiculo = await db.Vehiculos
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.UsuarioId == usuarioId, ct)
            ?? throw ExcepcionViajes.NoEncontrado("Vehicle not found.", "vehicle_not_found");

        return MapearVehiculo(vehiculo);
    }

    public async Task<VehiculoDto> UpsertVehiculoAsync(
        Guid usuarioId,
        SolicitudUpsertVehiculo solicitud,
        CancellationToken ct = default)
    {
        var usuario = await ObtenerUsuarioAsync(usuarioId, ct);
        AsegurarConductor(usuario);

        if (usuario.UniversidadId is not Guid universidadId)
        {
            throw ExcepcionViajes.Validacion("Driver has no university.", "missing_university");
        }

        var marca = (solicitud.MarcaModelo ?? string.Empty).Trim();
        var placa = (solicitud.Placa ?? string.Empty).Trim();
        var color = (solicitud.Color ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(marca))
        {
            throw ExcepcionViajes.Validacion("makeModel is required.");
        }

        if (string.IsNullOrWhiteSpace(placa))
        {
            throw ExcepcionViajes.Validacion("plate is required.");
        }

        if (string.IsNullOrWhiteSpace(color))
        {
            throw ExcepcionViajes.Validacion("color is required.");
        }

        if (solicitud.AsientosTotales < 1 || solicitud.AsientosTotales > 8)
        {
            throw ExcepcionViajes.Validacion(
                "seatsTotal must be between 1 and 8.",
                "invalid_seats");
        }

        var vehiculo = await db.Vehiculos.FirstOrDefaultAsync(v => v.UsuarioId == usuarioId, ct);
        var ahora = DateTimeOffset.UtcNow;

        if (vehiculo is null)
        {
            vehiculo = new Vehiculo
            {
                UniversidadId = universidadId,
                UsuarioId = usuarioId,
                MarcaModelo = marca,
                Placa = placa,
                Color = color,
                AsientosTotales = solicitud.AsientosTotales,
                CreadoEn = ahora,
                ActualizadoEn = ahora
            };
            db.Vehiculos.Add(vehiculo);
        }
        else
        {
            vehiculo.UniversidadId = universidadId;
            vehiculo.MarcaModelo = marca;
            vehiculo.Placa = placa;
            vehiculo.Color = color;
            vehiculo.AsientosTotales = solicitud.AsientosTotales;
            vehiculo.ActualizadoEn = ahora;
        }

        await db.SaveChangesAsync(ct);
        return MapearVehiculo(vehiculo);
    }

    public async Task<ViajeDto> PublicarViajeAsync(
        Guid usuarioId,
        SolicitudPublicarViaje solicitud,
        CancellationToken ct = default)
    {
        var usuario = await ObtenerUsuarioAsync(usuarioId, ct);
        AsegurarConductor(usuario);

        if (usuario.UniversidadId is not Guid universidadId)
        {
            throw ExcepcionViajes.Validacion("Driver has no university.", "missing_university");
        }

        if (solicitud.SaleEn == default)
        {
            throw ExcepcionViajes.Validacion("departureAt is required.");
        }

        var vehiculo = await db.Vehiculos.AsNoTracking()
            .FirstOrDefaultAsync(v => v.UsuarioId == usuarioId, ct)
            ?? throw ExcepcionViajes.Validacion(
                "Driver must have a vehicle before publishing a trip.",
                "vehicle_required");

        if (solicitud.AsientosDisponibles < 1
            || solicitud.AsientosDisponibles > vehiculo.AsientosTotales)
        {
            throw ExcepcionViajes.Validacion(
                $"seatsAvailable must be between 1 and {vehiculo.AsientosTotales}.",
                "invalid_seats");
        }

        var campus = await db.Sedes.AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Id == solicitud.CampusDestinoId && c.UniversidadId == universidadId,
                ct)
            ?? throw ExcepcionViajes.Validacion(
                "Destination campus not found for this university.",
                "campus_not_found");

        var waypoints = NormalizarWaypoints(solicitud, campus);
        ValidarWaypoints(waypoints);

        var wp0 = waypoints[0];
        var origenLat = wp0.Lat;
        var origenLng = wp0.Lng;
        var origenTexto = (solicitud.OrigenTexto ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(origenTexto))
        {
            origenTexto = string.IsNullOrWhiteSpace(wp0.Etiqueta)
                ? "Waypoint 1"
                : wp0.Etiqueta!.Trim();
        }

        var configuracion = await db.ConfiguracionesUniversidad.AsNoTracking()
            .FirstOrDefaultAsync(c => c.UniversidadId == universidadId, ct);

        var maxDiarios = configuracion?.MaxViajesDiariosPorConductor ?? 6;
        var diaUtc = solicitud.SaleEn.UtcDateTime.Date;
        var inicioDia = new DateTimeOffset(diaUtc, TimeSpan.Zero);
        var finDia = inicioDia.AddDays(1);

        var viajesDelDia = await db.Viajes.CountAsync(
            v => v.ConductorId == usuarioId
                 && v.SaleEn >= inicioDia
                 && v.SaleEn < finDia,
            ct);

        if (viajesDelDia >= maxDiarios)
        {
            throw ExcepcionViajes.Validacion(
                $"Daily trip limit of {maxDiarios} reached for this departure day.",
                "max_daily_trips");
        }

        var vias = waypoints.Count > 1
            ? waypoints.Skip(1).Select(w => (w.Lat, w.Lng)).ToList()
            : new List<(double Lat, double Lng)>();

        string? polilinea = null;
        decimal distanciaKm;

        try
        {
            var resultado = await directions.ObtenerRutaAsync(
                origenLat,
                origenLng,
                campus.Lat,
                campus.Lng,
                vias,
                ct);

            if (resultado is null || string.IsNullOrWhiteSpace(resultado.Polilinea))
            {
                distanciaKm = DistanciaHaversinePorWaypoints(waypoints, campus);
            }
            else
            {
                polilinea = resultado.Polilinea;
                distanciaKm = resultado.DistanciaKm;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            distanciaKm = DistanciaHaversinePorWaypoints(waypoints, campus);
        }

        if (distanciaKm <= 0)
        {
            distanciaKm = DistanciaHaversinePorWaypoints(waypoints, campus);
        }

        var ahora = DateTimeOffset.UtcNow;
        var viaje = new Viaje
        {
            UniversidadId = universidadId,
            ConductorId = usuarioId,
            CampusDestinoId = campus.Id,
            OrigenTexto = origenTexto,
            OrigenLat = origenLat,
            OrigenLng = origenLng,
            SaleEn = solicitud.SaleEn,
            AsientosDisponibles = solicitud.AsientosDisponibles,
            Polilinea = polilinea,
            DistanciaKm = distanciaKm,
            Estado = EstadoViaje.Programado,
            CreadoEn = ahora,
            ActualizadoEn = ahora
        };

        for (var i = 0; i < waypoints.Count; i++)
        {
            var wp = waypoints[i];
            viaje.PuntosRuta.Add(new PuntoRutaViaje
            {
                UniversidadId = universidadId,
                Seq = i,
                Lat = wp.Lat,
                Lng = wp.Lng,
                Etiqueta = string.IsNullOrWhiteSpace(wp.Etiqueta) ? null : wp.Etiqueta.Trim()
            });
        }

        db.Viajes.Add(viaje);
        await db.SaveChangesAsync(ct);

        try
        {
            await auditoria.EscribirAsync(
                $"trip.published:{viaje.Id}",
                TipoEventoAuditoria.Sistema,
                SeveridadAuditoria.Baja,
                universidadId,
                usuarioId,
                ct: ct);
        }
        catch
        {
            // Auditoría opcional: no fallar la publicación.
        }

        return MapearViaje(viaje);
    }

    public async Task<VistaPreviaRutaDto> PrevisualizarRutaAsync(
        Guid usuarioId,
        SolicitudVistaPreviaRuta solicitud,
        CancellationToken ct = default)
    {
        var usuario = await ObtenerUsuarioAsync(usuarioId, ct);
        AsegurarConductor(usuario);

        if (usuario.UniversidadId is not Guid universidadId)
        {
            throw ExcepcionViajes.Validacion("Driver has no university.", "missing_university");
        }

        var campus = await db.Sedes.AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Id == solicitud.CampusDestinoId && c.UniversidadId == universidadId,
                ct)
            ?? throw ExcepcionViajes.Validacion(
                "Destination campus not found for this university.",
                "campus_not_found");

        var publicar = new SolicitudPublicarViaje
        {
            Waypoints = solicitud.Waypoints,
            CampusDestinoId = solicitud.CampusDestinoId
        };
        var waypoints = NormalizarWaypoints(publicar, campus);
        ValidarWaypoints(waypoints);

        var wp0 = waypoints[0];
        var vias = waypoints.Count > 1
            ? waypoints.Skip(1).Select(w => (w.Lat, w.Lng)).ToList()
            : new List<(double Lat, double Lng)>();

        try
        {
            var resultado = await directions.ObtenerRutaAsync(
                wp0.Lat,
                wp0.Lng,
                campus.Lat,
                campus.Lng,
                vias,
                ct);

            if (resultado is null || string.IsNullOrWhiteSpace(resultado.Polilinea))
            {
                return new VistaPreviaRutaDto
                {
                    Polilinea = null,
                    DistanciaKm = DistanciaHaversinePorWaypoints(waypoints, campus),
                    DirectionsOk = false
                };
            }

            return new VistaPreviaRutaDto
            {
                Polilinea = resultado.Polilinea,
                DistanciaKm = resultado.DistanciaKm,
                DirectionsOk = true
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new VistaPreviaRutaDto
            {
                Polilinea = null,
                DistanciaKm = DistanciaHaversinePorWaypoints(waypoints, campus),
                DirectionsOk = false
            };
        }
    }

    public async Task<ViajeDto> IniciarViajeAsync(
        Guid usuarioId,
        Guid viajeId,
        CancellationToken ct = default)
    {
        var usuario = await ObtenerUsuarioAsync(usuarioId, ct);
        AsegurarConductor(usuario);

        var viaje = await ObtenerViajePropioAsync(usuarioId, viajeId, ct);

        if (viaje.Estado != EstadoViaje.Programado)
        {
            throw ExcepcionViajes.Conflicto(
                "Only scheduled trips can be started.",
                "invalid_trip_status");
        }

        var ahora = DateTimeOffset.UtcNow;
        viaje.Estado = EstadoViaje.EnCurso;
        viaje.IniciadoEn = ahora;
        viaje.ActualizadoEn = ahora;

        await db.SaveChangesAsync(ct);
        return MapearViaje(viaje);
    }

    public async Task<ViajeDto> CompletarViajeAsync(
        Guid usuarioId,
        Guid viajeId,
        CancellationToken ct = default)
    {
        var usuario = await ObtenerUsuarioAsync(usuarioId, ct);
        AsegurarConductor(usuario);

        var viaje = await ObtenerViajePropioAsync(usuarioId, viajeId, ct);

        if (viaje.Estado != EstadoViaje.EnCurso)
        {
            throw ExcepcionViajes.Conflicto(
                "Only in-progress trips can be completed.",
                "invalid_trip_status");
        }

        var configuracion = await db.ConfiguracionesUniversidad.AsNoTracking()
            .FirstOrDefaultAsync(c => c.UniversidadId == viaje.UniversidadId, ct);

        var ahora = DateTimeOffset.UtcNow;
        viaje.Estado = EstadoViaje.Completado;
        viaje.CompletadoEn = ahora;
        viaje.ActualizadoEn = ahora;
        viaje.Co2AhorradoKg = configuracion is { SeguimientoCo2Habilitado: true }
            ? decimal.Round(viaje.DistanciaKm * configuracion.FactorCo2KgKm, 3)
            : 0m;

        await db.SaveChangesAsync(ct);
        await motorEcoTokens.AlCompletarViajeAsync(viaje.Id, ct);
        return MapearViaje(viaje);
    }

    public async Task<ViajeDto> CancelarViajeAsync(
        Guid usuarioId,
        Guid viajeId,
        CancellationToken ct = default)
    {
        var usuario = await ObtenerUsuarioAsync(usuarioId, ct);
        AsegurarConductor(usuario);

        var viaje = await ObtenerViajePropioAsync(usuarioId, viajeId, ct);

        if (viaje.Estado == EstadoViaje.EnCurso)
        {
            throw ExcepcionViajes.Conflicto(
                "In-progress trips cannot be cancelled.",
                "invalid_trip_status");
        }

        if (viaje.Estado != EstadoViaje.Programado)
        {
            throw ExcepcionViajes.Conflicto(
                "Only scheduled trips can be cancelled.",
                "invalid_trip_status");
        }

        var ahora = DateTimeOffset.UtcNow;
        var esTardia = viaje.SaleEn - ahora < VentanaCancelacionTardia;

        viaje.Estado = EstadoViaje.Cancelado;
        viaje.ActualizadoEn = ahora;

        var solicitudes = await db.SolicitudesViaje
            .Where(s => s.ViajeId == viaje.Id
                        && (s.Estado == EstadoSolicitudViaje.Pendiente
                            || s.Estado == EstadoSolicitudViaje.Aceptada))
            .ToListAsync(ct);

        foreach (var solicitud in solicitudes)
        {
            solicitud.Estado = EstadoSolicitudViaje.CanceladaPorConductor;
            solicitud.ActualizadoEn = ahora;
        }

        await db.SaveChangesAsync(ct);

        if (esTardia)
        {
            try
            {
                await auditoria.EscribirAsync(
                    $"trip.late_cancel:{viaje.Id}",
                    TipoEventoAuditoria.Sistema,
                    SeveridadAuditoria.Media,
                    viaje.UniversidadId,
                    usuarioId,
                    ct: ct);
            }
            catch
            {
                // Auditoría no debe tumbar la cancelación.
            }

            await motorEcoTokens.AlCancelacionTardiaAsync(viaje.Id, usuarioId, ct);
        }

        return MapearViaje(viaje);
    }

    public async Task<MisViajesDto> ListarMisViajesAsync(
        Guid usuarioId,
        string? periodo = null,
        CancellationToken ct = default)
    {
        var usuario = await ObtenerUsuarioAsync(usuarioId, ct);
        var periodoNormalizado = NormalizarPeriodo(periodo);
        var ahora = DateTimeOffset.UtcNow;
        var desde = CalcularDesdePeriodo(periodoNormalizado, ahora);

        if (usuario.Rol == RolUsuario.Conductor)
        {
            var viajes = await db.Viajes.AsNoTracking()
                .Include(v => v.CampusDestino)
                .Where(v => v.ConductorId == usuarioId)
                .OrderByDescending(v => v.SaleEn)
                .ToListAsync(ct);

            var items = viajes.Select(v => MapearViajeMio(v, "driver", null)).ToList();

            var completados = viajes
                .Where(v => v.Estado == EstadoViaje.Completado
                            && v.CompletadoEn is DateTimeOffset c
                            && (desde is null || c >= desde.Value))
                .ToList();

            return new MisViajesDto
            {
                Viajes = items,
                Estadisticas = new EstadisticasViajesDto
                {
                    Periodo = periodoNormalizado,
                    Viajes = completados.Count,
                    Km = completados.Sum(v => v.DistanciaKm),
                    Co2Kg = completados.Sum(v => v.Co2AhorradoKg)
                }
            };
        }

        if (usuario.Rol == RolUsuario.Pasajero)
        {
            var solicitudes = await db.SolicitudesViaje.AsNoTracking()
                .Include(s => s.Viaje)
                .ThenInclude(v => v.CampusDestino)
                .Where(s => s.PasajeroId == usuarioId)
                .OrderByDescending(s => s.Viaje.SaleEn)
                .ToListAsync(ct);

            var items = solicitudes
                .Select(s => MapearViajeMio(
                    s.Viaje,
                    "passenger",
                    ConversorEnumDominio.ACadenaDb(s.Estado)))
                .ToList();

            var participacion = solicitudes
                .Where(s => s.Estado == EstadoSolicitudViaje.Aceptada
                            && s.Viaje.Estado == EstadoViaje.Completado
                            && s.Viaje.CompletadoEn is DateTimeOffset c
                            && (desde is null || c >= desde.Value))
                .Select(s => s.Viaje)
                .ToList();

            return new MisViajesDto
            {
                Viajes = items,
                Estadisticas = new EstadisticasViajesDto
                {
                    Periodo = periodoNormalizado,
                    Viajes = participacion.Count,
                    Km = participacion.Sum(v => v.DistanciaKm),
                    Co2Kg = participacion.Sum(v => v.Co2AhorradoKg)
                }
            };
        }

        throw ExcepcionViajes.Prohibido(
            "Only drivers and passengers can list their trips.",
            "mobile_only");
    }

    private async Task<Viaje> ObtenerViajePropioAsync(
        Guid conductorId,
        Guid viajeId,
        CancellationToken ct)
    {
        var viaje = await db.Viajes
            .Include(v => v.PuntosRuta)
            .FirstOrDefaultAsync(v => v.Id == viajeId, ct)
            ?? throw ExcepcionViajes.NoEncontrado("Trip not found.", "trip_not_found");

        if (viaje.ConductorId != conductorId)
        {
            throw ExcepcionViajes.Prohibido(
                "You can only manage your own trips.",
                "not_trip_owner");
        }

        return viaje;
    }

    private async Task<Usuario> ObtenerUsuarioAsync(Guid usuarioId, CancellationToken ct)
    {
        return await db.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == usuarioId, ct)
            ?? throw ExcepcionViajes.NoEncontrado("User not found.", "user_not_found");
    }

    private static void AsegurarConductor(Usuario usuario)
    {
        if (usuario.Rol != RolUsuario.Conductor)
        {
            throw ExcepcionViajes.Prohibido(
                "Only drivers can manage vehicles or publish trips.",
                "driver_only");
        }
    }

    private static void ValidarCoordenadasWaypoint(double lat, double lng, string codigo = "invalid_origin")
    {
        if (lat is < -90 or > 90 || lng is < -180 or > 180)
        {
            throw ExcepcionViajes.Validacion(
                "Waypoint lat must be in [-90, 90] and lng in [-180, 180].",
                codigo);
        }
    }

    private static List<WaypointDto> NormalizarWaypoints(SolicitudPublicarViaje solicitud, Campus campus)
    {
        var lista = solicitud.Waypoints?
            .Where(w => w is not null)
            .Select(w => new WaypointDto
            {
                Lat = w.Lat,
                Lng = w.Lng,
                Etiqueta = w.Etiqueta
            })
            .ToList() ?? new List<WaypointDto>();

        if (lista.Count == 0)
        {
            if (solicitud.OrigenLat is not double origenLat
                || solicitud.OrigenLng is not double origenLng)
            {
                return lista;
            }

            // Compat legacy: originLat/Lng → origin + midpoint hacia campus.
            ValidarCoordenadasWaypoint(origenLat, origenLng);
            lista.Add(new WaypointDto
            {
                Lat = origenLat,
                Lng = origenLng,
                Etiqueta = string.IsNullOrWhiteSpace(solicitud.OrigenTexto)
                    ? null
                    : solicitud.OrigenTexto.Trim()
            });
            lista.Add(new WaypointDto
            {
                Lat = (origenLat + campus.Lat) / 2.0,
                Lng = (origenLng + campus.Lng) / 2.0,
                Etiqueta = null
            });
        }

        return lista;
    }

    private static void ValidarWaypoints(IReadOnlyList<WaypointDto> waypoints)
    {
        if (waypoints.Count < 2 || waypoints.Count > 8)
        {
            throw ExcepcionViajes.Validacion(
                "waypoints must contain between 2 and 8 points.",
                "invalid_waypoints");
        }

        foreach (var wp in waypoints)
        {
            ValidarCoordenadasWaypoint(wp.Lat, wp.Lng);
        }
    }

    private static decimal DistanciaHaversinePorWaypoints(
        IReadOnlyList<WaypointDto> waypoints,
        Campus campus)
    {
        var puntos = waypoints
            .Select(w => (w.Lat, w.Lng))
            .Append((campus.Lat, campus.Lng))
            .ToList();
        return UtilidadHaversine.DistanciaALoLargoKm(puntos);
    }

    private static string NormalizarPeriodo(string? periodo)
    {
        var valor = (periodo ?? "total").Trim().ToLowerInvariant();
        return valor switch
        {
            "week" or "month" or "total" => valor,
            _ => throw ExcepcionViajes.Validacion(
                "period must be week, month, or total.",
                "invalid_period")
        };
    }

    private static DateTimeOffset? CalcularDesdePeriodo(string periodo, DateTimeOffset ahora) =>
        periodo switch
        {
            "week" => ahora.AddDays(-7),
            "month" => ahora.AddDays(-30),
            _ => null
        };

    private static VehiculoDto MapearVehiculo(Vehiculo v) => new()
    {
        Id = v.Id,
        MarcaModelo = v.MarcaModelo,
        Placa = v.Placa,
        Color = v.Color,
        AsientosTotales = v.AsientosTotales,
        UniversidadId = v.UniversidadId
    };

    private static ViajeDto MapearViaje(Viaje v) => new()
    {
        Id = v.Id,
        Estado = ConversorEnumDominio.ACadenaDb(v.Estado),
        OrigenTexto = v.OrigenTexto,
        OrigenLat = v.OrigenLat,
        OrigenLng = v.OrigenLng,
        CampusDestinoId = v.CampusDestinoId,
        SaleEn = v.SaleEn,
        AsientosDisponibles = v.AsientosDisponibles,
        Polilinea = v.Polilinea,
        DistanciaKm = v.DistanciaKm,
        Co2AhorradoKg = v.Co2AhorradoKg,
        ConductorId = v.ConductorId,
        UniversidadId = v.UniversidadId,
        Waypoints = v.PuntosRuta
            .OrderBy(p => p.Seq)
            .Select(p => new WaypointDto
            {
                Seq = p.Seq,
                Lat = p.Lat,
                Lng = p.Lng,
                Etiqueta = p.Etiqueta
            })
            .ToList()
    };

    private static ViajeMioDto MapearViajeMio(
        Viaje v,
        string rol,
        string? estadoSolicitud) => new()
    {
        Id = v.Id,
        Estado = ConversorEnumDominio.ACadenaDb(v.Estado),
        Rol = rol,
        EstadoSolicitud = estadoSolicitud,
        SaleEn = v.SaleEn,
        OrigenTexto = v.OrigenTexto,
        NombreCampusDestino = v.CampusDestino?.Nombre ?? string.Empty,
        DistanciaKm = v.DistanciaKm,
        Co2AhorradoKg = v.Co2AhorradoKg
    };
}
