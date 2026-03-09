using OTG.Domain.Common;

namespace OTG.Domain.Teams;

public sealed class Team : AuditableEntity
{
    public required string HackathonId { get; init; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public required string LeaderId { get; init; }
    public int MaxSize { get; set; }
    public List<string> Skills { get; set; } = [];
    public List<TeamMember> Members { get; set; } = [];
}

public sealed class TeamMember
{
    public required string UserId { get; init; }
    public DateTimeOffset JoinedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
