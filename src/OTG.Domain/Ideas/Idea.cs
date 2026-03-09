using OTG.Domain.Common;

namespace OTG.Domain.Ideas;

public sealed class Idea : AuditableEntity
{
    public required string HackathonId { get; init; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public IdeaStatus Status { get; set; } = IdeaStatus.Draft;
    public required string AuthorId { get; init; }
    public string? TeamId { get; set; }
    public string? TrackId { get; set; }
    public List<string> Tags { get; set; } = [];
    public List<string> Attachments { get; set; } = [];
    public string? VideoUrl { get; set; }
    public string? RepoUrl { get; set; }
    public string? DemoUrl { get; set; }
    public int Votes { get; set; }
    public DateTimeOffset? SubmittedAtUtc { get; set; }
    public List<string> AssignedJudgeIds { get; set; } = [];
    public List<string> AwardIds { get; set; } = [];
    public bool TermsAccepted { get; set; }
}
