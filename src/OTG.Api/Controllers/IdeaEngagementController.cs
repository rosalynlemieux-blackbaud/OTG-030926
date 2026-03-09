using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OTG.Api.Contracts;
using OTG.Api.Extensions;
using OTG.Application.Abstractions.Repositories;
using OTG.Domain.Ideas;

namespace OTG.Api.Controllers;

[ApiController]
[Route("api/ideas/{ideaId}")]
[Authorize(Policy = "NotBanned")]
public sealed class IdeaEngagementController(
    IIdeaRepository ideaRepository,
    ICommentRepository commentRepository,
    IRatingRepository ratingRepository) : ControllerBase
{
    [HttpPost("comments")]
    public async Task<ActionResult<Comment>> CreateComment(
        string ideaId,
        [FromQuery] string hackathonId,
        UpsertCommentRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var idea = await ideaRepository.GetByIdAsync(ideaId, hackathonId, cancellationToken);
        if (idea is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest("Comment content is required.");
        }

        var existingComments = await commentRepository.GetByIdeaAsync(ideaId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.ParentId))
        {
            var parent = existingComments.FirstOrDefault(item => string.Equals(item.Id, request.ParentId, StringComparison.Ordinal));
            if (parent is null)
            {
                return BadRequest("Parent comment was not found for this idea.");
            }

            if (!string.IsNullOrWhiteSpace(parent.ParentId))
            {
                return BadRequest("Only one level of threaded replies is supported.");
            }
        }

        var comment = new Comment
        {
            Id = Guid.NewGuid().ToString("N"),
            IdeaId = ideaId,
            AuthorId = userId,
            Content = request.Content.Trim(),
            ParentId = request.ParentId,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await commentRepository.UpsertAsync(comment, cancellationToken);
        return Ok(comment);
    }

    [HttpPut("comments/{commentId}")]
    public async Task<ActionResult<Comment>> UpdateComment(
        string ideaId,
        string commentId,
        [FromQuery] string hackathonId,
        UpdateCommentRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var idea = await ideaRepository.GetByIdAsync(ideaId, hackathonId, cancellationToken);
        if (idea is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest("Comment content is required.");
        }

        var existingComments = await commentRepository.GetByIdeaAsync(ideaId, cancellationToken);
        var comment = existingComments.FirstOrDefault(item => string.Equals(item.Id, commentId, StringComparison.Ordinal));
        if (comment is null)
        {
            return NotFound();
        }

        if (!User.IsInRole("Admin") && !string.Equals(comment.AuthorId, userId, StringComparison.Ordinal))
        {
            return Forbid();
        }

        comment.Content = request.Content.Trim();
        comment.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await commentRepository.UpsertAsync(comment, cancellationToken);
        return Ok(comment);
    }

    [HttpGet("comments")]
    public async Task<ActionResult<IReadOnlyList<Comment>>> GetComments(
        string ideaId,
        [FromQuery] string hackathonId,
        CancellationToken cancellationToken)
    {
        var idea = await ideaRepository.GetByIdAsync(ideaId, hackathonId, cancellationToken);
        if (idea is null)
        {
            return NotFound();
        }

        var comments = await commentRepository.GetByIdeaAsync(ideaId, cancellationToken);
        if (User.IsInRole("Admin"))
        {
            return Ok(comments);
        }

        return Ok(comments.Where(comment => !comment.IsModerated).ToList());
    }

    [HttpGet("ratings")]
    public async Task<ActionResult<IReadOnlyList<IdeaRating>>> GetRatings(
        string ideaId,
        [FromQuery] string hackathonId,
        CancellationToken cancellationToken)
    {
        var idea = await ideaRepository.GetByIdAsync(ideaId, hackathonId, cancellationToken);
        if (idea is null)
        {
            return NotFound();
        }

        var ratings = await ratingRepository.GetByIdeaAsync(ideaId, cancellationToken);
        if (User.IsInRole("Admin"))
        {
            return Ok(ratings);
        }

        return Ok(ratings.Where(rating => !rating.IsModerated).ToList());
    }
}
