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

    [JsonPropertyName("transactions")]
    public IReadOnlyList<TransaccionEcoDto> Transactions { get; init; } = [];

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; init; }
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
