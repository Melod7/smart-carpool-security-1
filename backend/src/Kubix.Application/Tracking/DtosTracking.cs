using System.Text.Json.Serialization;

namespace Kubix.Application.Tracking;

public sealed class SolicitudPing
{
    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lng")]
    public double Lng { get; set; }
}

public sealed class PingDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("tripId")]
    public Guid ViajeId { get; set; }

    [JsonPropertyName("userId")]
    public Guid UsuarioId { get; set; }

    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lng")]
    public double Lng { get; set; }

    [JsonPropertyName("recordedAt")]
    public DateTimeOffset RegistradoEn { get; set; }
}

public sealed class ParticipanteTrackingDto
{
    [JsonPropertyName("userId")]
    public Guid UsuarioId { get; set; }

    [JsonPropertyName("role")]
    public string Rol { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Nombre { get; set; }

    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lng")]
    public double Lng { get; set; }

    [JsonPropertyName("recordedAt")]
    public DateTimeOffset? RegistradoEn { get; set; }

    /// <summary>"ping" si hay ubicación registrada; "pickup" si se usa el punto de recogida.</summary>
    [JsonPropertyName("source")]
    public string Fuente { get; set; } = "ping";
}

public sealed class TrackingWaypointDto
{
    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lng")]
    public double Lng { get; set; }

    [JsonPropertyName("seq")]
    public int Seq { get; set; }

    [JsonPropertyName("label")]
    public string? Etiqueta { get; set; }
}

public sealed class TrackingViajeDto
{
    [JsonPropertyName("tripId")]
    public Guid ViajeId { get; set; }

    [JsonPropertyName("status")]
    public string Estado { get; set; } = string.Empty;

    [JsonPropertyName("polyline")]
    public string? Polilinea { get; set; }

    [JsonPropertyName("waypoints")]
    public IReadOnlyList<TrackingWaypointDto> Waypoints { get; set; } = [];

    [JsonPropertyName("participants")]
    public IReadOnlyList<ParticipanteTrackingDto> Participantes { get; set; } = [];
}

public sealed class TrackingAdminActivoDto
{
    [JsonPropertyName("trips")]
    public IReadOnlyList<TrackingViajeDto> Viajes { get; set; } = [];
}
