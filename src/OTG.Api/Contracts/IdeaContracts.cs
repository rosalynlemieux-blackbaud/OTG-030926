using OTG.Domain.Ideas;

namespace OTG.Api.Contracts;

public sealed class UpsertIdeaRequest
{
    public string? Id { get; init; }
    public required string HackathonId { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public IdeaStatus Status { get; init; } = IdeaStatus.Draft;
    public string? TeamId { get; init; }
    public string? TrackId { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<string> Attachments { get; init; } = [];
    public string? VideoUrl { get; init; }
    public string? RepoUrl { get; init; }
    public string? DemoUrl { get; init; }
    public bool TermsAccepted { get; init; }
}
