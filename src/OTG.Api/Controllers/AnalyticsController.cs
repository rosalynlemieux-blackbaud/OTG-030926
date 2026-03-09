using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OTG.Application.Abstractions.Repositories;

namespace OTG.Api.Controllers;

[ApiController]
[Route("api/analytics")]
[Authorize(Roles = "Admin")]
[Authorize(Policy = "NotBanned")]
public sealed class AnalyticsController(
    IIdeaRepository ideaRepository,
    ITeamRepository teamRepository,
    IUserRepository userRepository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<object>> GetSummary([FromQuery] string hackathonId, CancellationToken cancellationToken)
    {
        var ideas = await ideaRepository.SearchAsync(hackathonId, null, null, null, cancellationToken);
        var teams = await teamRepository.SearchAsync(hackathonId, null, cancellationToken);
        var users = await userRepository.SearchAsync(null, 1000, cancellationToken);

        var ideasByStatus = ideas
            .GroupBy(idea => idea.Status.ToString().ToLowerInvariant(), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var teamSizes = teams
            .Select(team => new
            {
                TeamId = team.Id,
                TeamName = team.Name,
                MemberCount = team.Members.Count
            })
            .ToList();

        var totalParticipants = users.Count(user => user.Roles.Contains(OTG.Domain.Identity.UserRole.Participant));
        var totalJudges = users.Count(user => user.Roles.Contains(OTG.Domain.Identity.UserRole.Judge));

        return Ok(new
        {
            HackathonId = hackathonId,
            TotalIdeas = ideas.Count,
            TotalTeams = teams.Count,
            TotalParticipants = totalParticipants,
            TotalJudges = totalJudges,
            IdeasByStatus = ideasByStatus,
            TeamSizes = teamSizes
        });
    }
}
