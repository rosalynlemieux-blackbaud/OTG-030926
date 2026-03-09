using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OTG.Api.Contracts;
using OTG.Api.Extensions;
using OTG.Application.Abstractions.Repositories;
using OTG.Domain.Identity;

namespace OTG.Api.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize(Policy = "NotBanned")]
public sealed class ProfileController(IUserRepository userRepository) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<Profile>> GetMyProfile(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user?.Profile is null)
        {
            return NotFound();
        }

        return Ok(user.Profile);
    }

    [HttpPut("me")]
    public async Task<ActionResult<Profile>> UpdateMyProfile(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        user.Profile ??= new Profile
        {
            Id = Guid.NewGuid().ToString("N"),
            UserId = user.Id,
            Email = user.Email
        };

        user.Profile.Name = request.Name ?? user.Profile.Name;
        user.Profile.AvatarUrl = request.AvatarUrl ?? user.Profile.AvatarUrl;
        user.Profile.Department = request.Department ?? user.Profile.Department;
        user.Profile.Location = request.Location ?? user.Profile.Location;
        user.Profile.Skills = request.Skills.ToList();
        user.Profile.Interests = request.Interests.ToList();
        user.Profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
        user.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await userRepository.UpsertAsync(user, cancellationToken);
        return Ok(user.Profile);
    }
}
