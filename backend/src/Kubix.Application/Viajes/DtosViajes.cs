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
}

public sealed class SolicitudPublicarViaje
{
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

    [JsonPropertyName("driverId")]
    public Guid ConductorId { get; set; }

    [JsonPropertyName("universityId")]
    public Guid UniversidadId { get; set; }
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
