namespace OTG.Api.Contracts;

public sealed class SparkIdeaRequest
{
    public string? ConversationId { get; init; }
    public required string HackathonId { get; init; }
    public required string Message { get; init; }
}

public sealed class SparkIdeaResponse
{
    public required string ConversationId { get; init; }
    public required string Reply { get; init; }
    public bool ReadyToSubmit { get; init; }
    public string? IdeaId { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
}
