using OTG.Domain.Teams;

namespace OTG.Infrastructure.Cosmos.Documents;

public sealed class TeamDocument : CosmosDocument
{
    public required string HackathonId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? ImageUrl { get; init; }
    public required string LeaderId { get; init; }
    public int MaxSize { get; init; }
    public List<string> Skills { get; init; } = [];
    public List<TeamMember> Members { get; init; } = [];
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed class TeamJoinRequestDocument : CosmosDocument
{
    public required string TeamId { get; init; }
    public required string UserId { get; init; }
    public string? Message { get; init; }
    public TeamJoinRequestStatus Status { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed class TeamInviteDocument : CosmosDocument
{
    public required string TeamId { get; init; }
    public required string Email { get; init; }
    public required string InvitedBy { get; init; }
    public required string Token { get; init; }
    public TeamInviteStatus Status { get; init; }
    public bool NeedsApproval { get; init; }
    public string? ApprovedBy { get; init; }
    public DateTimeOffset? ExpiresAtUtc { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}
