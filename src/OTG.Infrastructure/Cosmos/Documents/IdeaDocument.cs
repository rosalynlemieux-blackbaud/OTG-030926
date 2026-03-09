using OTG.Domain.Ideas;

namespace OTG.Infrastructure.Cosmos.Documents;

public sealed class IdeaDocument : CosmosDocument
{
    public required string HackathonId { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public IdeaStatus Status { get; init; }
    public required string AuthorId { get; init; }
    public string? TeamId { get; init; }
    public string? TrackId { get; init; }
    public List<string> Tags { get; init; } = [];
    public List<string> Attachments { get; init; } = [];
    public string? VideoUrl { get; init; }
    public string? RepoUrl { get; init; }
    public string? DemoUrl { get; init; }
    public int Votes { get; init; }
    public DateTimeOffset? SubmittedAtUtc { get; init; }
    public List<string> AssignedJudgeIds { get; init; } = [];
    public List<string> AwardIds { get; init; } = [];
    public bool TermsAccepted { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed class CommentDocument : CosmosDocument
{
    public required string IdeaId { get; init; }
    public required string AuthorId { get; init; }
    public required string Content { get; init; }
    public string? ParentId { get; init; }
    public bool IsModerated { get; init; }
    public string? ModerationReason { get; init; }
    public string? ModeratedBy { get; init; }
    public DateTimeOffset? ModeratedAtUtc { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed class RatingDocument : CosmosDocument
{
    public required string IdeaId { get; init; }
    public required string JudgeId { get; init; }
    public List<CriterionScore> Scores { get; init; } = [];
    public string? OverallFeedback { get; init; }
    public decimal WeightedScore { get; init; }
    public bool IsModerated { get; init; }
    public string? ModerationReason { get; init; }
    public string? ModeratedBy { get; init; }
    public DateTimeOffset? ModeratedAtUtc { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}
