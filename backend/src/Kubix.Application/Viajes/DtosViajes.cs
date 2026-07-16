using System.Text.Json.Serialization;

namespace Kubix.Application.Viajes;

public sealed class VehiculoDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("makeModel")]
    public string MarcaModelo { get; set; } = string.Empty;

    [JsonPropertyName("plate")]
    public string Placa { get; set; } = string.Empty;

    [JsonPropertyName("color")]
    public string Color { get; set; } = string.Empty;

    [JsonPropertyName("seatsTotal")]
    public int AsientosTotales { get; set; }

    [JsonPropertyName("image")]
    public string? Imagen { get; set; }

    [JsonPropertyName("universityId")]
    public Guid UniversidadId { get; set; }
}

public sealed class SolicitudUpsertVehiculo
{
    [JsonPropertyName("makeModel")]
    public string MarcaModelo { get; set; } = string.Empty;

    [JsonPropertyName("plate")]
    public string Placa { get; set; } = string.Empty;

    [JsonPropertyName("color")]
    public string Color { get; set; } = string.Empty;

    [JsonPropertyName("seatsTotal")]
    public int AsientosTotales { get; set; }

    [JsonPropertyName("image")]
    public string? Imagen { get; set; }
}

/// <summary>Respuesta cuando el cambio de vehículo queda pendiente de aprobación.</summary>
public sealed class CambioVehiculoPendienteDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("status")]
    public string Estado { get; set; } = "pending";

    [JsonPropertyName("kind")]
    public string Tipo { get; set; } = "vehicle_change";

    [JsonPropertyName("message")]
    public string Mensaje { get; set; } =
        "Cambio de vehículo enviado al coordinador para aprobación.";
}

public sealed class ResultadoUpsertVehiculo
{
    public VehiculoDto? Vehiculo { get; set; }
    public CambioVehiculoPendienteDto? CambioPendiente { get; set; }
}

public sealed class WaypointDto
{
    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lng")]
    public double Lng { get; set; }

    [JsonPropertyName("label")]
    public string? Etiqueta { get; set; }

    [JsonPropertyName("seq")]
    public int? Seq { get; set; }
}

public sealed class SolicitudPublicarViaje
{
    [JsonPropertyName("originText")]
    public string OrigenTexto { get; set; } = string.Empty;

    [JsonPropertyName("originLat")]
    public double? OrigenLat { get; set; }

    [JsonPropertyName("originLng")]
    public double? OrigenLng { get; set; }

    [JsonPropertyName("waypoints")]
    public List<WaypointDto>? Waypoints { get; set; }

    [JsonPropertyName("destinationCampusId")]
    public Guid CampusDestinoId { get; set; }

    [JsonPropertyName("departureAt")]
    public DateTimeOffset SaleEn { get; set; }

    [JsonPropertyName("seatsAvailable")]
    public int AsientosDisponibles { get; set; }
}

/// Vista previa de Directions sin persistir el viaje (mapa del conductor).
public sealed class SolicitudVistaPreviaRuta
{
    [JsonPropertyName("waypoints")]
    public List<WaypointDto>? Waypoints { get; set; }

    [JsonPropertyName("destinationCampusId")]
    public Guid CampusDestinoId { get; set; }
}

public sealed class VistaPreviaRutaDto
{
    [JsonPropertyName("polyline")]
    public string? Polilinea { get; set; }

    [JsonPropertyName("distanceKm")]
    public decimal DistanciaKm { get; set; }

    [JsonPropertyName("directionsOk")]
    public bool DirectionsOk { get; set; }
}

public sealed class PuntoEsperaSugeridoDto
{
    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lng")]
    public double Lng { get; set; }

    [JsonPropertyName("distanceM")]
    public double DistanciaM { get; set; }

    [JsonPropertyName("segmentIndex")]
    public int SegmentIndex { get; set; }

    [JsonPropertyName("tooFar")]
    public bool TooFar { get; set; }
}

public sealed class ViajeDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("status")]
    public string Estado { get; set; } = string.Empty;

    [JsonPropertyName("originText")]
    public string OrigenTexto { get; set; } = string.Empty;

    [JsonPropertyName("originLat")]
    public double OrigenLat { get; set; }

    [JsonPropertyName("originLng")]
    public double OrigenLng { get; set; }

    [JsonPropertyName("destinationCampusId")]
    public Guid CampusDestinoId { get; set; }

    [JsonPropertyName("departureAt")]
    public DateTimeOffset SaleEn { get; set; }

    [JsonPropertyName("seatsAvailable")]
    public int AsientosDisponibles { get; set; }

    [JsonPropertyName("polyline")]
    public string? Polilinea { get; set; }

    [JsonPropertyName("distanceKm")]
    public decimal DistanciaKm { get; set; }

    [JsonPropertyName("co2SavedKg")]
    public decimal Co2AhorradoKg { get; set; }

    [JsonPropertyName("driverId")]
    public Guid ConductorId { get; set; }

    [JsonPropertyName("universityId")]
    public Guid UniversidadId { get; set; }

    [JsonPropertyName("waypoints")]
    public IReadOnlyList<WaypointDto> Waypoints { get; set; } = Array.Empty<WaypointDto>();

    [JsonPropertyName("suggestedWait")]
    public PuntoEsperaSugeridoDto? EsperaSugerida { get; set; }
}

public sealed class MisViajesDto
{
    [JsonPropertyName("trips")]
    public IReadOnlyList<ViajeMioDto> Viajes { get; set; } = Array.Empty<ViajeMioDto>();

    [JsonPropertyName("stats")]
    public EstadisticasViajesDto Estadisticas { get; set; } = new();
}

public sealed class ViajeMioDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("status")]
    public string Estado { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Rol { get; set; } = string.Empty;

    [JsonPropertyName("requestStatus")]
    public string? EstadoSolicitud { get; set; }

    [JsonPropertyName("departureAt")]
    public DateTimeOffset SaleEn { get; set; }

    [JsonPropertyName("originText")]
    public string OrigenTexto { get; set; } = string.Empty;

    [JsonPropertyName("destinationCampusName")]
    public string NombreCampusDestino { get; set; } = string.Empty;

    [JsonPropertyName("distanceKm")]
    public decimal DistanciaKm { get; set; }

    [JsonPropertyName("co2SavedKg")]
    public decimal Co2AhorradoKg { get; set; }
}

public sealed class EstadisticasViajesDto
{
    [JsonPropertyName("period")]
    public string Periodo { get; set; } = "total";

    [JsonPropertyName("trips")]
    public int Viajes { get; set; }

    [JsonPropertyName("km")]
    public decimal Km { get; set; }

    [JsonPropertyName("co2Kg")]
    public decimal Co2Kg { get; set; }
}

public sealed class SolicitudCrearSolicitudViaje
{
    [JsonPropertyName("pickupText")]
    public string RecogidaTexto { get; set; } = string.Empty;

    [JsonPropertyName("pickupLat")]
    public double RecogidaLat { get; set; }

    [JsonPropertyName("pickupLng")]
    public double RecogidaLng { get; set; }
}

public sealed class SolicitudViajeDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("tripId")]
    public Guid ViajeId { get; set; }

    [JsonPropertyName("passengerId")]
    public Guid PasajeroId { get; set; }

    [JsonPropertyName("pickupText")]
    public string RecogidaTexto { get; set; } = string.Empty;

    [JsonPropertyName("pickupLat")]
    public double RecogidaLat { get; set; }

    [JsonPropertyName("pickupLng")]
    public double RecogidaLng { get; set; }

    [JsonPropertyName("status")]
    public string Estado { get; set; } = string.Empty;

    [JsonPropertyName("universityId")]
    public Guid UniversidadId { get; set; }
}
