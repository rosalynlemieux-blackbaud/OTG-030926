using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OTG.Api.Contracts;
using OTG.Api.Extensions;
using OTG.Application.Abstractions.Repositories;
using OTG.Domain.Teams;

namespace OTG.Api.Controllers;

[ApiController]
[Route("api/teams")]
[Authorize(Policy = "NotBanned")]
public sealed class TeamsController(ITeamRepository teamRepository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Team>>> Search(
        [FromQuery] string hackathonId,
        [FromQuery] string? query,
        CancellationToken cancellationToken)
    {
        var teams = await teamRepository.SearchAsync(hackathonId, query, cancellationToken);
        return Ok(teams);
    }

    [HttpGet("{teamId}")]
    public async Task<ActionResult<Team>> GetById(
        string teamId,
        [FromQuery] string hackathonId,
        CancellationToken cancellationToken)
    {
        var team = await teamRepository.GetByIdAsync(teamId, hackathonId, cancellationToken);
        return team is null ? NotFound() : Ok(team);
    }

    [HttpPost]
    public async Task<ActionResult<Team>> Upsert(UpsertTeamRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        Team team;
        if (!string.IsNullOrWhiteSpace(request.Id))
        {
            var existing = await teamRepository.GetByIdAsync(request.Id, request.HackathonId, cancellationToken);
            if (existing is null)
            {
                return NotFound();
            }

            if (!string.Equals(existing.LeaderId, userId, StringComparison.Ordinal) && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            existing.Name = request.Name;
            existing.Description = request.Description;
            existing.ImageUrl = request.ImageUrl;
            existing.MaxSize = request.MaxSize;
            existing.Skills = request.Skills.ToList();
            existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
            team = existing;
        }
        else
        {
            team = new Team
            {
                Id = Guid.NewGuid().ToString("N"),
                HackathonId = request.HackathonId,
                Name = request.Name,
                Description = request.Description,
                ImageUrl = request.ImageUrl,
                LeaderId = userId,
                MaxSize = request.MaxSize,
                Skills = request.Skills.ToList(),
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };

            team.Members.Add(new TeamMember { UserId = userId });
        }

        await teamRepository.UpsertAsync(team, cancellationToken);
        return Ok(team);
    }
}
