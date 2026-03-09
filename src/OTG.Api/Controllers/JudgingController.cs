using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OTG.Api.Contracts;
using OTG.Api.Extensions;
using OTG.Application.Abstractions.Repositories;
using OTG.Domain.Ideas;

namespace OTG.Api.Controllers;

[ApiController]
[Route("api/judging")]
[Authorize(Policy = "NotBanned")]
public sealed class JudgingController(
    IIdeaRepository ideaRepository,
    IRatingRepository ratingRepository,
    IHackathonRepository hackathonRepository) : ControllerBase
{
    [HttpGet("assigned")]
    [Authorize(Roles = "Judge,Admin")]
    public async Task<ActionResult<IReadOnlyList<Idea>>> GetAssignedIdeas([FromQuery] string hackathonId, CancellationToken cancellationToken)
    {
        var ideas = await ideaRepository.SearchAsync(hackathonId, IdeaStatus.Submitted, null, null, cancellationToken);
        if (User.IsInRole("Admin"))
        {
            return Ok(ideas);
        }

        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        return Ok(ideas.Where(idea => idea.AssignedJudgeIds.Contains(userId, StringComparer.Ordinal)).ToList());
    }

    [HttpPost("ratings")]
    [Authorize(Roles = "Judge,Admin")]
    public async Task<ActionResult<IdeaRating>> SubmitRating(SubmitIdeaRatingRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var idea = await ideaRepository.GetByIdAsync(request.IdeaId, request.HackathonId, cancellationToken);
        if (idea is null)
        {
            return NotFound();
        }

        if (!User.IsInRole("Admin") && !idea.AssignedJudgeIds.Contains(userId, StringComparer.Ordinal))
        {
            return Forbid();
        }

        var existingRatings = await ratingRepository.GetByIdeaAsync(idea.Id, cancellationToken);
        var existing = existingRatings.FirstOrDefault(item => string.Equals(item.JudgeId, userId, StringComparison.Ordinal));

        var weightedScore = await CalculateWeightedScoreAsync(request.HackathonId, request.Scores, cancellationToken);
        var rating = new IdeaRating
        {
            Id = existing?.Id ?? Guid.NewGuid().ToString("N"),
            IdeaId = idea.Id,
            JudgeId = userId,
            Scores = request.Scores.ToList(),
            OverallFeedback = request.OverallFeedback,
            WeightedScore = weightedScore,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await ratingRepository.UpsertAsync(rating, cancellationToken);
        return Ok(rating);
    }

    private async Task<decimal> CalculateWeightedScoreAsync(string hackathonId, IReadOnlyList<CriterionScore> scores, CancellationToken cancellationToken)
    {
        if (scores.Count == 0)
        {
            return 0m;
        }

        var hackathon = await hackathonRepository.GetByIdAsync(hackathonId, cancellationToken);
        if (hackathon is null || hackathon.JudgingCriteria.Count == 0)
        {
            return (decimal)scores.Average(score => score.Score);
        }

        var criteriaMap = hackathon.JudgingCriteria.ToDictionary(c => c.Id, StringComparer.Ordinal);
        decimal weightedTotal = 0m;
        decimal totalWeight = 0m;

        foreach (var score in scores)
        {
            if (!criteriaMap.TryGetValue(score.CriterionId, out var criterion))
            {
                continue;
            }

            var normalized = criterion.MaxScore <= 0 ? 0m : (decimal)score.Score / criterion.MaxScore;
            weightedTotal += normalized * criterion.Weight;
            totalWeight += criterion.Weight;
        }

        if (totalWeight <= 0)
        {
            return 0m;
        }

        return Math.Round((weightedTotal / totalWeight) * 100m, 2, MidpointRounding.AwayFromZero);
    }
}
