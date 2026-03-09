using OTG.Domain.Hackathons;

namespace OTG.Infrastructure.Cosmos.Documents;

public sealed class HackathonDocument : CosmosDocument
{
    public required string HackathonId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? LogoUrl { get; init; }
    public string? LedeImageUrl { get; init; }
    public DateTimeOffset? RegistrationOpen { get; init; }
    public DateTimeOffset? SubmissionDeadline { get; init; }
    public DateTimeOffset? JudgingStart { get; init; }
    public DateTimeOffset? JudgingEnd { get; init; }
    public string? RulesMarkdown { get; init; }
    public List<FaqEntry> Faq { get; init; } = [];
    public string? Terms { get; init; }
    public string? SwagHtml { get; init; }
    public List<Track> Tracks { get; init; } = [];
    public List<Award> Awards { get; init; } = [];
    public List<JudgingCriterion> JudgingCriteria { get; init; } = [];
    public List<Milestone> Milestones { get; init; } = [];
    public LeaderboardBandSettings? LeaderboardBands { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}
