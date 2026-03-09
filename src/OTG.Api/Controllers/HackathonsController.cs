using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OTG.Application.Abstractions.Repositories;

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
}
