using OTG.Domain.Ideas;

namespace OTG.Api.Contracts;

public sealed class SubmitIdeaRatingRequest
{
    public required string IdeaId { get; init; }
    public required string HackathonId { get; init; }
    public IReadOnlyList<CriterionScore> Scores { get; init; } = [];
    public string? OverallFeedback { get; init; }
}

public sealed class AssignJudgeRequest
{
    public required string IdeaId { get; init; }
    public required string HackathonId { get; init; }
    public required string JudgeUserId { get; init; }
}

public sealed class MarkWinnerRequest
{
    public required string IdeaId { get; init; }
    public required string HackathonId { get; init; }
    public IReadOnlyList<string> AwardIds { get; init; } = [];
}
