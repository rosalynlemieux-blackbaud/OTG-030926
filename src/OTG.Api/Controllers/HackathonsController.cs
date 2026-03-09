using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OTG.Api.Contracts;
using OTG.Application.Abstractions.Repositories;
using OTG.Domain.Hackathons;

namespace OTG.Api.Controllers;

[ApiController]
[Route("api/hackathons")]
public sealed class HackathonsController(IHackathonRepository hackathonRepository) : ControllerBase
{
    [HttpGet("{hackathonId}")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> GetById(string hackathonId, CancellationToken cancellationToken)
    {
        var hackathon = await hackathonRepository.GetByIdAsync(hackathonId, cancellationToken);
        return hackathon is null ? NotFound() : Ok(hackathon);
    }

    [HttpPut("{hackathonId}/leaderboard-bands")]
    [Authorize(Policy = "NotBanned", Roles = "Admin")]
    public async Task<ActionResult<LeaderboardBandSettings>> UpdateLeaderboardBands(
        string hackathonId,
        UpdateLeaderboardBandsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.PlatinumMinPercentile > 100m
            || request.GoldMinPercentile > 100m
            || request.SilverMinPercentile > 100m
            || request.SilverMinPercentile < 0m
            || request.GoldMinPercentile < 0m
            || request.PlatinumMinPercentile < 0m
            || request.PlatinumMinPercentile < request.GoldMinPercentile
            || request.GoldMinPercentile < request.SilverMinPercentile)
        {
            return BadRequest("Leaderboard bands must be within 0..100 and ordered Platinum >= Gold >= Silver.");
        }

        var hackathon = await hackathonRepository.GetByIdAsync(hackathonId, cancellationToken);
        if (hackathon is null)
        {
            return NotFound();
        }

        hackathon.LeaderboardBands = new LeaderboardBandSettings
        {
            PlatinumMinPercentile = request.PlatinumMinPercentile,
            GoldMinPercentile = request.GoldMinPercentile,
            SilverMinPercentile = request.SilverMinPercentile
        };
        hackathon.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await hackathonRepository.UpsertAsync(hackathon, cancellationToken);
        return Ok(hackathon.LeaderboardBands);
    }
}
