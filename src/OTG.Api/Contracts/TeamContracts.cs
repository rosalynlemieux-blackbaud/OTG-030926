namespace OTG.Api.Contracts;

public sealed class UpsertTeamRequest
{
    public string? Id { get; init; }
    public required string HackathonId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? ImageUrl { get; init; }
    public int MaxSize { get; init; } = 5;
    public IReadOnlyList<string> Skills { get; init; } = [];
}
