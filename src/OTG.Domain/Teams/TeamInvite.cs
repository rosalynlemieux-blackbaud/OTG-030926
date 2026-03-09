using OTG.Domain.Common;

namespace OTG.Domain.Teams;

public enum TeamInviteStatus
{
    Pending,
    Accepted,
    Declined,
    Expired
}

public sealed class TeamInvite : AuditableEntity
{
    public required string TeamId { get; init; }
    public required string Email { get; init; }
    public required string InvitedBy { get; init; }
    public required string Token { get; init; }
    public TeamInviteStatus Status { get; set; } = TeamInviteStatus.Pending;
    public bool NeedsApproval { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
}
