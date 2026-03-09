using OTG.Domain.Common;

namespace OTG.Domain.Hackathons;

public sealed class Hackathon : AuditableEntity
{
    public required string HackathonId { get; init; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string? LedeImageUrl { get; set; }
    public DateTimeOffset? RegistrationOpen { get; set; }
    public DateTimeOffset? SubmissionDeadline { get; set; }
    public DateTimeOffset? JudgingStart { get; set; }
    public DateTimeOffset? JudgingEnd { get; set; }
    public string? RulesMarkdown { get; set; }
    public List<FaqEntry> Faq { get; set; } = [];
    public string? Terms { get; set; }
    public string? SwagHtml { get; set; }
    public List<Track> Tracks { get; set; } = [];
    public List<Award> Awards { get; set; } = [];
    public List<JudgingCriterion> JudgingCriteria { get; set; } = [];
    public List<Milestone> Milestones { get; set; } = [];
}

public sealed class FaqEntry
{
    public required string Question { get; init; }
    public required string Answer { get; init; }
}
