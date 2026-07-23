using System.Text.Json.Serialization;

namespace Kubix.Application.EcoTokens;

public sealed class ResumenEcoDto
{
    [JsonPropertyName("balance")]
    public int Balance { get; init; }

    [JsonPropertyName("lifetime")]
    public int Lifetime { get; init; }

    [JsonPropertyName("level")]
    public string Level { get; init; } = string.Empty;

    [JsonPropertyName("progress")]
    public double? Progress { get; init; }

    [JsonPropertyName("gamificationEnabled")]
    public bool GamificationEnabled { get; init; }

    [JsonPropertyName("prizes")]
    public IReadOnlyList<PremioEcoDto> Prizes { get; init; } = [];

    [JsonPropertyName("transactions")]
    public IReadOnlyList<TransaccionEcoDto> Transactions { get; init; } = [];

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; init; }
}

public sealed class PremioEcoDto
{
    [JsonPropertyName("code")]
    public string Codigo { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Nombre { get; init; } = string.Empty;

    [JsonPropertyName("cost")]
    public int Costo { get; init; }
}

public sealed class SolicitudCanjePremio
{
    [JsonPropertyName("prizeCode")]
    public string CodigoPremio { get; set; } = string.Empty;
}

public sealed class ResultadoCanjePremioDto
{
    [JsonPropertyName("prizeCode")]
    public string CodigoPremio { get; init; } = string.Empty;

    [JsonPropertyName("prizeName")]
    public string NombrePremio { get; init; } = string.Empty;

    [JsonPropertyName("cost")]
    public int Costo { get; init; }

    [JsonPropertyName("balance")]
    public int Balance { get; init; }

    [JsonPropertyName("message")]
    public string Mensaje { get; init; } = string.Empty;
}

public sealed class CanjeAdminDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("userId")]
    public Guid UsuarioId { get; init; }

    [JsonPropertyName("userName")]
    public string NombreUsuario { get; init; } = string.Empty;

    [JsonPropertyName("prizeCode")]
    public string CodigoPremio { get; init; } = string.Empty;

    [JsonPropertyName("prizeName")]
    public string NombrePremio { get; init; } = string.Empty;

    [JsonPropertyName("cost")]
    public int Costo { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreadoEn { get; init; }
}

public sealed class TransaccionEcoDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("amount")]
    public int Amount { get; init; }

    [JsonPropertyName("sourceId")]
    public string SourceId { get; init; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }
}
