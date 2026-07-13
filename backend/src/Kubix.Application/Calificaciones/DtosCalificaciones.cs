using System.Text.Json.Serialization;

namespace Kubix.Application.Calificaciones;

public sealed class SolicitudCrearCalificacion
{
    [JsonPropertyName("ratedUserId")]
    public Guid CalificadoId { get; set; }

    [JsonPropertyName("stars")]
    public int Estrellas { get; set; }

    [JsonPropertyName("comment")]
    public string? Comentario { get; set; }
}

public sealed class CalificacionDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("tripId")]
    public Guid ViajeId { get; set; }

    [JsonPropertyName("raterId")]
    public Guid CalificadorId { get; set; }

    [JsonPropertyName("ratedUserId")]
    public Guid CalificadoId { get; set; }

    [JsonPropertyName("stars")]
    public int Estrellas { get; set; }

    [JsonPropertyName("comment")]
    public string? Comentario { get; set; }

    [JsonPropertyName("universityId")]
    public Guid UniversidadId { get; set; }
}

public sealed class CalificacionPendienteDto
{
    [JsonPropertyName("tripId")]
    public Guid ViajeId { get; set; }

    [JsonPropertyName("ratedUserId")]
    public Guid CalificadoId { get; set; }

    [JsonPropertyName("ratedUserName")]
    public string NombreCalificado { get; set; } = string.Empty;

    [JsonPropertyName("roleToRate")]
    public string RolACalificar { get; set; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public DateTimeOffset? ExpiraEn { get; set; }
}
