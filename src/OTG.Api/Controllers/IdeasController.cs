using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OTG.Api.Authorization;
using OTG.Api.Contracts;
using OTG.Api.Extensions;
using OTG.Application.Abstractions.Repositories;
using OTG.Domain.Ideas;

namespace OTG.Api.Controllers;

[ApiController]
[Route("api/ideas")]
[Authorize(Policy = "NotBanned")]
public sealed class IdeasController(IIdeaRepository ideaRepository, IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Idea>>> Search(
        [FromQuery] string hackathonId,
        [FromQuery] IdeaStatus? status,
        [FromQuery] string? trackId,
        [FromQuery] string? searchText,
        CancellationToken cancellationToken)
    {
        var ideas = await ideaRepository.SearchAsync(hackathonId, status, trackId, searchText, cancellationToken);
        return Ok(ideas);
    }

    [HttpGet("{ideaId}")]
    public async Task<ActionResult<Idea>> GetById(
        string ideaId,
        [FromQuery] string hackathonId,
        CancellationToken cancellationToken)
    {
        var idea = await ideaRepository.GetByIdAsync(ideaId, hackathonId, cancellationToken);
        return idea is null ? NotFound() : Ok(idea);
    }

    [HttpPost]
    public async Task<ActionResult<Idea>> Upsert(UpsertIdeaRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        Idea idea;
        if (!string.IsNullOrWhiteSpace(request.Id))
        {
            var existing = await ideaRepository.GetByIdAsync(request.Id, request.HackathonId, cancellationToken);
            if (existing is null)
            {
                return NotFound();
            }

            var authorizationResult = await authorizationService.AuthorizeAsync(User, existing, new IdeaOwnerOrAdminRequirement());
            if (!authorizationResult.Succeeded)
            {
                return Forbid();
            }

            existing.Title = request.Title;
            existing.Description = request.Description;
            existing.Status = request.Status;
            existing.TeamId = request.TeamId;
            existing.TrackId = request.TrackId;
            existing.Tags = request.Tags.ToList();
            existing.Attachments = request.Attachments.ToList();
            existing.VideoUrl = request.VideoUrl;
            existing.RepoUrl = request.RepoUrl;
            existing.DemoUrl = request.DemoUrl;
            existing.TermsAccepted = request.TermsAccepted;
            if (existing.Status == IdeaStatus.Submitted && existing.SubmittedAtUtc is null)
            {
                existing.SubmittedAtUtc = DateTimeOffset.UtcNow;
            }

            existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
            idea = existing;
        }
        else
        {
            idea = new Idea
            {
                Id = Guid.NewGuid().ToString("N"),
                HackathonId = request.HackathonId,
                Title = request.Title,
                Description = request.Description,
                Status = request.Status,
                AuthorId = userId,
                TeamId = request.TeamId,
                TrackId = request.TrackId,
                Tags = request.Tags.ToList(),
                Attachments = request.Attachments.ToList(),
                VideoUrl = request.VideoUrl,
                RepoUrl = request.RepoUrl,
                DemoUrl = request.DemoUrl,
                TermsAccepted = request.TermsAccepted,
                SubmittedAtUtc = request.Status == IdeaStatus.Submitted ? DateTimeOffset.UtcNow : null,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };
        }

        await ideaRepository.UpsertAsync(idea, cancellationToken);
        return Ok(idea);
    }
}
