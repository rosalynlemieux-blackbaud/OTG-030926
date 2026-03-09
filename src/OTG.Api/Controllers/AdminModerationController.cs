using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OTG.Api.Contracts;
using OTG.Api.Extensions;
using OTG.Application.Abstractions.Repositories;
using OTG.Domain.Ideas;

namespace OTG.Api.Controllers;

[ApiController]
[Route("api/admin/moderation")]
[Authorize(Policy = "NotBanned", Roles = "Admin")]
public sealed class AdminModerationController(
    IIdeaRepository ideaRepository,
    ICommentRepository commentRepository,
    IRatingRepository ratingRepository) : ControllerBase
{
    [HttpPut("comments/{commentId}")]
    public async Task<ActionResult<Comment>> ModerateComment(
        string commentId,
        [FromQuery] string ideaId,
        [FromQuery] string hackathonId,
        ModerateContentRequest request,
        CancellationToken cancellationToken)
    {
        var moderatorId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(moderatorId))
        {
            return Unauthorized();
        }

        var idea = await ideaRepository.GetByIdAsync(ideaId, hackathonId, cancellationToken);
        if (idea is null)
        {
            return NotFound();
        }

        var comments = await commentRepository.GetByIdeaAsync(ideaId, cancellationToken);
        var comment = comments.FirstOrDefault(item => string.Equals(item.Id, commentId, StringComparison.Ordinal));
        if (comment is null)
        {
            return NotFound();
        }

        comment.IsModerated = request.IsModerated;
        comment.ModerationReason = request.IsModerated ? request.Reason : null;
        comment.ModeratedBy = request.IsModerated ? moderatorId : null;
        comment.ModeratedAtUtc = request.IsModerated ? DateTimeOffset.UtcNow : null;
        comment.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await commentRepository.UpsertAsync(comment, cancellationToken);
        return Ok(comment);
    }

    [HttpPut("ratings/{ratingId}")]
    public async Task<ActionResult<IdeaRating>> ModerateRating(
        string ratingId,
        [FromQuery] string ideaId,
        [FromQuery] string hackathonId,
        ModerateContentRequest request,
        CancellationToken cancellationToken)
    {
        var moderatorId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(moderatorId))
        {
            return Unauthorized();
        }

        var idea = await ideaRepository.GetByIdAsync(ideaId, hackathonId, cancellationToken);
        if (idea is null)
        {
            return NotFound();
        }

        var ratings = await ratingRepository.GetByIdeaAsync(ideaId, cancellationToken);
        var rating = ratings.FirstOrDefault(item => string.Equals(item.Id, ratingId, StringComparison.Ordinal));
        if (rating is null)
        {
            return NotFound();
        }

        rating.IsModerated = request.IsModerated;
        rating.ModerationReason = request.IsModerated ? request.Reason : null;
        rating.ModeratedBy = request.IsModerated ? moderatorId : null;
        rating.ModeratedAtUtc = request.IsModerated ? DateTimeOffset.UtcNow : null;
        rating.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await ratingRepository.UpsertAsync(rating, cancellationToken);
        return Ok(rating);
    }
}
