using OTG.Domain.Common;

namespace OTG.Domain.Ideas;

public sealed class IdeaRating : AuditableEntity
{
    public required string IdeaId { get; init; }
    public required string JudgeId { get; init; }
    public List<CriterionScore> Scores { get; set; } = [];
    public string? OverallFeedback { get; set; }
    public decimal WeightedScore { get; set; }
    public bool IsModerated { get; set; }
    public string? ModerationReason { get; set; }
    public string? ModeratedBy { get; set; }
    public DateTimeOffset? ModeratedAtUtc { get; set; }
}

public sealed class CriterionScore
{
    public required string CriterionId { get; init; }
    public int Score { get; init; }
    public string? Feedback { get; init; }
}
