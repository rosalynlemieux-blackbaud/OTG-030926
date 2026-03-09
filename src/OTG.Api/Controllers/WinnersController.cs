using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OTG.Application.Abstractions.Repositories;
using OTG.Domain.Ideas;

namespace OTG.Api.Controllers;

[ApiController]
[Route("api/winners")]
[Authorize(Policy = "NotBanned")]
public sealed class WinnersController(
    IIdeaRepository ideaRepository,
    IHackathonRepository hackathonRepository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<object>> GetWinners([FromQuery] string hackathonId, CancellationToken cancellationToken)
    {
        var ideas = await ideaRepository.SearchAsync(hackathonId, IdeaStatus.Winner, null, null, cancellationToken);
        var hackathon = await hackathonRepository.GetByIdAsync(hackathonId, cancellationToken);

        var trackNames = hackathon?.Tracks.ToDictionary(track => track.Id, track => track.Name, StringComparer.Ordinal)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);

        var grouped = ideas
            .Where(idea => !string.IsNullOrWhiteSpace(idea.TrackId))
            .GroupBy(idea => idea.TrackId!, StringComparer.Ordinal)
            .Select(group => new
            {
                TrackId = group.Key,
                TrackName = trackNames.TryGetValue(group.Key, out var name) ? name : group.Key,
                Winners = group.Select(idea => new
                {
                    idea.Id,
                    idea.Title,
                    idea.Description,
                    idea.AwardIds,
                    idea.AuthorId,
                    idea.TeamId,
                    idea.TrackId
                }).ToList()
            })
            .OrderBy(item => item.TrackName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var specialRecognition = ideas
            .Where(idea => string.IsNullOrWhiteSpace(idea.TrackId))
            .Select(idea => new
            {
                idea.Id,
                idea.Title,
                idea.Description,
                idea.AwardIds,
                idea.AuthorId,
                idea.TeamId,
                idea.TrackId
            })
            .ToList();

        return Ok(new
        {
            HackathonId = hackathonId,
            Tracks = grouped,
            SpecialRecognition = specialRecognition
        });
    }
}
