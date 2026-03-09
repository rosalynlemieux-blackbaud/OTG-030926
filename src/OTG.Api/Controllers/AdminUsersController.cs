using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OTG.Api.Contracts;
using OTG.Api.Extensions;
using OTG.Application.Abstractions.Repositories;
using OTG.Domain.Identity;

namespace OTG.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
[Authorize(Policy = "NotBanned")]
public sealed class AdminUsersController(IUserRepository userRepository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<object>>> SearchUsers([FromQuery] string? query, CancellationToken cancellationToken)
    {
        var users = await userRepository.SearchAsync(query, 50, cancellationToken);
        return Ok(users.Select(user => new
        {
            user.Id,
            user.Email,
            Roles = user.Roles.Select(role => role.ToString().ToLowerInvariant()),
            Name = user.Profile?.Name,
            Department = user.Profile?.Department,
            Banned = user.Profile?.Banned ?? false
        }).ToList());
    }

    [HttpPut("{userId}/roles")]
    public async Task<ActionResult<object>> UpdateRoles(string userId, UpdateUserRolesRequest request, CancellationToken cancellationToken)
    {
        var currentUserId = User.GetUserId();
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        var parsedRoles = new List<UserRole>();
        foreach (var roleText in request.Roles)
        {
            if (!Enum.TryParse<UserRole>(roleText, ignoreCase: true, out var role))
            {
                return BadRequest($"Invalid role '{roleText}'.");
            }

            if (!parsedRoles.Contains(role))
            {
                parsedRoles.Add(role);
            }
        }

        if (string.Equals(currentUserId, userId, StringComparison.Ordinal) && !parsedRoles.Contains(UserRole.Admin))
        {
            return BadRequest("You cannot remove your own admin role.");
        }

        var isRemovingAdmin = user.Roles.Contains(UserRole.Admin) && !parsedRoles.Contains(UserRole.Admin);
        if (isRemovingAdmin)
        {
            var adminsRemaining = await CountActiveAdminsAsync(cancellationToken);
            if (adminsRemaining <= 1)
            {
                return Conflict("At least one unbanned admin must remain.");
            }
        }

        user.Roles = parsedRoles.Count == 0 ? [UserRole.Participant] : parsedRoles;
        user.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await userRepository.UpsertAsync(user, cancellationToken);
        return Ok(new
        {
            user.Id,
            Roles = user.Roles.Select(role => role.ToString().ToLowerInvariant())
        });
    }

    [HttpPut("{userId}/ban")]
    public async Task<ActionResult<object>> SetBanStatus(string userId, SetUserBanRequest request, CancellationToken cancellationToken)
    {
        var currentUserId = User.GetUserId();
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        if (request.Banned && string.Equals(currentUserId, userId, StringComparison.Ordinal))
        {
            return BadRequest("You cannot ban your own account.");
        }

        if (request.Banned && user.Roles.Contains(UserRole.Admin))
        {
            var adminsRemaining = await CountActiveAdminsAsync(cancellationToken);
            if (adminsRemaining <= 1)
            {
                return Conflict("At least one unbanned admin must remain.");
            }
        }

        user.Profile ??= new Profile
        {
            Id = Guid.NewGuid().ToString("N"),
            UserId = user.Id,
            Email = user.Email
        };

        user.Profile.Banned = request.Banned;
        user.Profile.BannedAtUtc = request.Banned ? DateTimeOffset.UtcNow : null;
        user.Profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
        user.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await userRepository.UpsertAsync(user, cancellationToken);
        return Ok(new
        {
            user.Id,
            user.Profile.Banned,
            user.Profile.BannedAtUtc
        });
    }

    private async Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken)
    {
        var users = await userRepository.SearchAsync(null, 100, cancellationToken);
        return users.Count(user => user.Roles.Contains(UserRole.Admin) && user.Profile?.Banned is not true);
    }
}
