using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OTG.Api.Contracts;
using OTG.Application.Abstractions.Repositories;
using OTG.Domain.Ideas;

namespace OTG.Api.Controllers;

[ApiController]
[Route("api/admin/judging")]
[Authorize(Roles = "Admin")]
[Authorize(Policy = "NotBanned")]
public sealed class AdminJudgingController(IIdeaRepository ideaRepository) : ControllerBase
{
    [HttpPost("assign-judge")]
    public async Task<ActionResult<Idea>> AssignJudge(AssignJudgeRequest request, CancellationToken cancellationToken)
    {
        var idea = await ideaRepository.GetByIdAsync(request.IdeaId, request.HackathonId, cancellationToken);
        if (idea is null)
        {
            return NotFound();
        }

        if (!idea.AssignedJudgeIds.Contains(request.JudgeUserId, StringComparer.Ordinal))
        {
            idea.AssignedJudgeIds.Add(request.JudgeUserId);
            idea.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await ideaRepository.UpsertAsync(idea, cancellationToken);
        }

        return Ok(idea);
    }

    [HttpPost("mark-winner")]
    public async Task<ActionResult<Idea>> MarkWinner(MarkWinnerRequest request, CancellationToken cancellationToken)
    {
        var idea = await ideaRepository.GetByIdAsync(request.IdeaId, request.HackathonId, cancellationToken);
        if (idea is null)
        {
            return NotFound();
        }

        idea.Status = IdeaStatus.Winner;
        idea.AwardIds = request.AwardIds.Distinct(StringComparer.Ordinal).ToList();
        idea.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await ideaRepository.UpsertAsync(idea, cancellationToken);
        return Ok(idea);
    }
}
