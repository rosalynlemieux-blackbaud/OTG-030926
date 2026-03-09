namespace OTG.Api.Services;

public interface ISparkIdeaService
{
    SparkIdeaResult Generate(string conversationId, string message);
}

public sealed class SparkIdeaResult
{
    public required string Reply { get; init; }
    public bool ReadyToSubmit { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
}
