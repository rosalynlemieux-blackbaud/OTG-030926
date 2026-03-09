using OTG.Domain.Common;

namespace OTG.Domain.Teams;

public enum TeamJoinRequestStatus
{
    Pending,
    Approved,
    Rejected
}

public sealed class TeamJoinRequest : AuditableEntity
{
    public required string TeamId { get; init; }
    public required string UserId { get; init; }
    public string? Message { get; set; }
    public TeamJoinRequestStatus Status { get; set; } = TeamJoinRequestStatus.Pending;
}
