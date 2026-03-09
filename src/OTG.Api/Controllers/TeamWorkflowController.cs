using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OTG.Api.Contracts;
using OTG.Api.Extensions;
using OTG.Application.Abstractions.Repositories;
using OTG.Domain.Teams;

namespace OTG.Api.Controllers;

[ApiController]
[Route("api/teams/{teamId}")]
[Authorize(Policy = "NotBanned")]
public sealed class TeamWorkflowController(
    ITeamRepository teamRepository,
    ITeamJoinRequestRepository joinRequestRepository,
    ITeamInviteRepository inviteRepository,
    IUserRepository userRepository) : ControllerBase
{
    [HttpPost("join-requests")]
    public async Task<ActionResult<TeamJoinRequest>> CreateJoinRequest(string teamId, [FromQuery] string hackathonId, CreateJoinRequestRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var team = await teamRepository.GetByIdAsync(teamId, hackathonId, cancellationToken);
        if (team is null)
        {
            return NotFound();
        }

        if (team.Members.Any(member => string.Equals(member.UserId, userId, StringComparison.Ordinal)))
        {
            return Conflict("User is already a team member.");
        }

        var existing = await joinRequestRepository.GetByTeamAsync(teamId, cancellationToken);
        if (existing.Any(item => string.Equals(item.UserId, userId, StringComparison.Ordinal) && item.Status == TeamJoinRequestStatus.Pending))
        {
            return Conflict("A pending join request already exists.");
        }

        var joinRequest = new TeamJoinRequest
        {
            Id = Guid.NewGuid().ToString("N"),
            TeamId = teamId,
            UserId = userId,
            Message = request.Message,
            Status = TeamJoinRequestStatus.Pending,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await joinRequestRepository.UpsertAsync(joinRequest, cancellationToken);
        return Ok(joinRequest);
    }

    [HttpGet("join-requests")]
    public async Task<ActionResult<IReadOnlyList<TeamJoinRequest>>> GetJoinRequests(string teamId, [FromQuery] string hackathonId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var team = await teamRepository.GetByIdAsync(teamId, hackathonId, cancellationToken);
        if (team is null)
        {
            return NotFound();
        }

        if (!IsLeaderOrAdmin(team, userId))
        {
            return Forbid();
        }

        var requests = await joinRequestRepository.GetByTeamAsync(teamId, cancellationToken);
        return Ok(requests);
    }

    [HttpPost("join-requests/{requestId}/approve")]
    public async Task<ActionResult<TeamJoinRequest>> ApproveJoinRequest(string teamId, string requestId, [FromQuery] string hackathonId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var team = await teamRepository.GetByIdAsync(teamId, hackathonId, cancellationToken);
        if (team is null)
        {
            return NotFound();
        }

        if (!IsLeaderOrAdmin(team, userId))
        {
            return Forbid();
        }

        var requests = await joinRequestRepository.GetByTeamAsync(teamId, cancellationToken);
        var joinRequest = requests.FirstOrDefault(item => string.Equals(item.Id, requestId, StringComparison.Ordinal));
        if (joinRequest is null)
        {
            return NotFound();
        }

        if (joinRequest.Status != TeamJoinRequestStatus.Pending)
        {
            return Conflict("Only pending requests can be approved.");
        }

        if (team.Members.Count >= team.MaxSize)
        {
            return Conflict("Team is full.");
        }

        if (!team.Members.Any(member => string.Equals(member.UserId, joinRequest.UserId, StringComparison.Ordinal)))
        {
            team.Members.Add(new TeamMember { UserId = joinRequest.UserId });
        }

        joinRequest.Status = TeamJoinRequestStatus.Approved;
        joinRequest.UpdatedAtUtc = DateTimeOffset.UtcNow;
        team.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await joinRequestRepository.UpsertAsync(joinRequest, cancellationToken);
        await teamRepository.UpsertAsync(team, cancellationToken);
        return Ok(joinRequest);
    }

    [HttpPost("join-requests/{requestId}/reject")]
    public async Task<ActionResult<TeamJoinRequest>> RejectJoinRequest(string teamId, string requestId, [FromQuery] string hackathonId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var team = await teamRepository.GetByIdAsync(teamId, hackathonId, cancellationToken);
        if (team is null)
        {
            return NotFound();
        }

        if (!IsLeaderOrAdmin(team, userId))
        {
            return Forbid();
        }

        var requests = await joinRequestRepository.GetByTeamAsync(teamId, cancellationToken);
        var joinRequest = requests.FirstOrDefault(item => string.Equals(item.Id, requestId, StringComparison.Ordinal));
        if (joinRequest is null)
        {
            return NotFound();
        }

        joinRequest.Status = TeamJoinRequestStatus.Rejected;
        joinRequest.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await joinRequestRepository.UpsertAsync(joinRequest, cancellationToken);
        return Ok(joinRequest);
    }

    [HttpPost("invites")]
    public async Task<ActionResult<TeamInvite>> CreateInvite(string teamId, [FromQuery] string hackathonId, CreateInviteRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var team = await teamRepository.GetByIdAsync(teamId, hackathonId, cancellationToken);
        if (team is null)
        {
            return NotFound();
        }

        if (!IsLeaderOrAdmin(team, userId))
        {
            return Forbid();
        }

        if (team.Members.Count >= team.MaxSize)
        {
            return Conflict("Team is full.");
        }

        var invite = new TeamInvite
        {
            Id = Guid.NewGuid().ToString("N"),
            TeamId = teamId,
            Email = request.Email.Trim().ToLowerInvariant(),
            InvitedBy = userId,
            Token = Guid.NewGuid().ToString("N"),
            Status = TeamInviteStatus.Pending,
            NeedsApproval = request.NeedsApproval,
            ExpiresAtUtc = request.ExpiresAtUtc,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await inviteRepository.UpsertAsync(invite, cancellationToken);
        return Ok(invite);
    }

    [HttpGet("invites")]
    public async Task<ActionResult<IReadOnlyList<TeamInvite>>> GetInvites(string teamId, [FromQuery] string hackathonId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var team = await teamRepository.GetByIdAsync(teamId, hackathonId, cancellationToken);
        if (team is null)
        {
            return NotFound();
        }

        if (!IsLeaderOrAdmin(team, userId))
        {
            return Forbid();
        }

        var invites = await inviteRepository.GetByTeamAsync(teamId, cancellationToken);
        return Ok(invites);
    }

    [HttpPost("invites/{inviteId}/approve")]
    public async Task<ActionResult<TeamInvite>> ApproveInvite(string teamId, string inviteId, [FromQuery] string hackathonId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var team = await teamRepository.GetByIdAsync(teamId, hackathonId, cancellationToken);
        if (team is null)
        {
            return NotFound();
        }

        if (!IsLeaderOrAdmin(team, userId))
        {
            return Forbid();
        }

        var invites = await inviteRepository.GetByTeamAsync(teamId, cancellationToken);
        var invite = invites.FirstOrDefault(item => string.Equals(item.Id, inviteId, StringComparison.Ordinal));
        if (invite is null)
        {
            return NotFound();
        }

        invite.ApprovedBy = userId;
        invite.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await inviteRepository.UpsertAsync(invite, cancellationToken);
        return Ok(invite);
    }

    [HttpPost("invites/accept")]
    public async Task<ActionResult<TeamInvite>> AcceptInvite(string teamId, [FromQuery] string hackathonId, [FromQuery] string token, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var team = await teamRepository.GetByIdAsync(teamId, hackathonId, cancellationToken);
        if (team is null)
        {
            return NotFound();
        }

        var invite = await inviteRepository.GetByTokenAsync(token, teamId, cancellationToken);
        if (invite is null)
        {
            return NotFound();
        }

        if (invite.Status != TeamInviteStatus.Pending)
        {
            return Conflict("Invite is no longer pending.");
        }

        if (invite.ExpiresAtUtc.HasValue && invite.ExpiresAtUtc.Value < DateTimeOffset.UtcNow)
        {
            invite.Status = TeamInviteStatus.Expired;
            invite.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await inviteRepository.UpsertAsync(invite, cancellationToken);
            return Conflict("Invite has expired.");
        }

        if (!string.Equals(invite.Email, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        if (invite.NeedsApproval && string.IsNullOrWhiteSpace(invite.ApprovedBy))
        {
            return Conflict("Invite requires leader approval before acceptance.");
        }

        if (team.Members.Count >= team.MaxSize)
        {
            return Conflict("Team is full.");
        }

        if (!team.Members.Any(member => string.Equals(member.UserId, userId, StringComparison.Ordinal)))
        {
            team.Members.Add(new TeamMember { UserId = userId });
            team.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await teamRepository.UpsertAsync(team, cancellationToken);
        }

        invite.Status = TeamInviteStatus.Accepted;
        invite.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await inviteRepository.UpsertAsync(invite, cancellationToken);

        return Ok(invite);
    }

    private bool IsLeaderOrAdmin(Team team, string userId)
    {
        return string.Equals(team.LeaderId, userId, StringComparison.Ordinal) || User.IsInRole("Admin");
    }
}
