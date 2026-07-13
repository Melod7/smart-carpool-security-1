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
    IEscritorAuditoria auditoria) : IServicioViajes
{
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

        ValidarCoordenadasOrigen(solicitud.OrigenLat, solicitud.OrigenLng);

        var origenTexto = (solicitud.OrigenTexto ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(origenTexto))
        {
            throw ExcepcionViajes.Validacion("originText is required.");
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

        string? polilinea = null;
        decimal distanciaKm;

        try
        {
            var resultado = await directions.ObtenerRutaAsync(
                solicitud.OrigenLat,
                solicitud.OrigenLng,
                campus.Lat,
                campus.Lng,
                ct);

            if (resultado is null || string.IsNullOrWhiteSpace(resultado.Polilinea))
            {
                distanciaKm = UtilidadHaversine.DistanciaKm(
                    solicitud.OrigenLat,
                    solicitud.OrigenLng,
                    campus.Lat,
                    campus.Lng);
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
            distanciaKm = UtilidadHaversine.DistanciaKm(
                solicitud.OrigenLat,
                solicitud.OrigenLng,
                campus.Lat,
                campus.Lng);
        }

        if (distanciaKm <= 0)
        {
            distanciaKm = UtilidadHaversine.DistanciaKm(
                solicitud.OrigenLat,
                solicitud.OrigenLng,
                campus.Lat,
                campus.Lng);
        }

        var ahora = DateTimeOffset.UtcNow;
        var viaje = new Viaje
        {
            UniversidadId = universidadId,
            ConductorId = usuarioId,
            CampusDestinoId = campus.Id,
            OrigenTexto = origenTexto,
            OrigenLat = solicitud.OrigenLat,
            OrigenLng = solicitud.OrigenLng,
            SaleEn = solicitud.SaleEn,
            AsientosDisponibles = solicitud.AsientosDisponibles,
            Polilinea = polilinea,
            DistanciaKm = distanciaKm,
            Estado = EstadoViaje.Programado,
            CreadoEn = ahora,
            ActualizadoEn = ahora
        };

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

    private static void ValidarCoordenadasOrigen(double lat, double lng)
    {
        if (lat is < -90 or > 90 || lng is < -180 or > 180)
        {
            throw ExcepcionViajes.Validacion(
                "originLat must be in [-90, 90] and originLng in [-180, 180].",
                "invalid_origin");
        }
    }

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
        ConductorId = v.ConductorId,
        UniversidadId = v.UniversidadId
    };
}
