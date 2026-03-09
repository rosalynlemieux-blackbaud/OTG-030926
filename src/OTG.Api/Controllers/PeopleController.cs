using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OTG.Application.Abstractions.Repositories;

namespace OTG.Api.Controllers;

[ApiController]
[Route("api/people")]
[Authorize(Policy = "NotBanned")]
public sealed class PeopleController(IUserRepository userRepository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<object>>> Search([FromQuery] string? query, [FromQuery] int limit = 50, CancellationToken cancellationToken = default)
    {
        var boundedLimit = Math.Clamp(limit, 1, 100);
        var users = await userRepository.SearchAsync(query, boundedLimit, cancellationToken);
        return Ok(users.Select(user => new
        {
            user.Id,
            Name = user.Profile?.Name,
            user.Email,
            Avatar = user.Profile?.AvatarUrl,
            Department = user.Profile?.Department,
            Location = user.Profile?.Location,
            Roles = user.Roles.Select(role => role.ToString().ToLowerInvariant()).ToList(),
            Skills = user.Profile?.Skills ?? [],
            TopSkills = (user.Profile?.Skills ?? []).Take(3).ToList()
        }).ToList());
    }
}
