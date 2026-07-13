using System.Text.Json.Serialization;

namespace Kubix.Application.Sos;

public sealed class SolicitudCrearSos
{
    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lng")]
    public double Lng { get; set; }

    [JsonPropertyName("tripId")]
    public Guid? ViajeId { get; set; }
}

public sealed class AlertaSosDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("status")]
    public string Estado { get; set; } = string.Empty;

    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lng")]
    public double Lng { get; set; }

    [JsonPropertyName("tripId")]
    public Guid? ViajeId { get; set; }

    [JsonPropertyName("firedAt")]
    public DateTimeOffset DisparadaEn { get; set; }

    [JsonPropertyName("resolvedBy")]
    public Guid? ResueltaPor { get; set; }

    [JsonPropertyName("resolvedAt")]
    public DateTimeOffset? ResueltaEn { get; set; }

    [JsonPropertyName("universityId")]
    public Guid UniversidadId { get; set; }

    [JsonPropertyName("userId")]
    public Guid UsuarioId { get; set; }
}

public sealed class AlertaSosAdminDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("status")]
    public string Estado { get; set; } = string.Empty;

    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lng")]
    public double Lng { get; set; }

    [JsonPropertyName("firedAt")]
    public DateTimeOffset DisparadaEn { get; set; }

    [JsonPropertyName("resolvedBy")]
    public Guid? ResueltaPor { get; set; }

    [JsonPropertyName("resolvedAt")]
    public DateTimeOffset? ResueltaEn { get; set; }

    [JsonPropertyName("student")]
    public ParticipanteSosDto Estudiante { get; set; } = null!;

    [JsonPropertyName("trip")]
    public ViajeSosDto? Viaje { get; set; }

    [JsonPropertyName("driver")]
    public ParticipanteSosDto? Conductor { get; set; }
}

public sealed class ParticipanteSosDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Correo { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Rol { get; set; } = string.Empty;
}

public sealed class ViajeSosDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("status")]
    public string Estado { get; set; } = string.Empty;

    [JsonPropertyName("originText")]
    public string OrigenTexto { get; set; } = string.Empty;

    [JsonPropertyName("departureAt")]
    public DateTimeOffset SaleEn { get; set; }
}
