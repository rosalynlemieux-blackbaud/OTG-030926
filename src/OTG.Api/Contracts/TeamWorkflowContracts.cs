namespace OTG.Api.Contracts;

public sealed class CreateJoinRequestRequest
{
    public string? Message { get; init; }
}

public sealed class CreateInviteRequest
{
    public required string Email { get; init; }
    public bool NeedsApproval { get; init; }
    public DateTimeOffset? ExpiresAtUtc { get; init; }
}
