using System.Text.Json.Serialization;

namespace OTG.Infrastructure.Cosmos.Documents;

public abstract class CosmosDocument
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("_etag")]
    public string? ETag { get; init; }
}
