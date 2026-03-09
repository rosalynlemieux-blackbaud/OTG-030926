using OTG.Domain.Identity;

namespace OTG.Infrastructure.Cosmos.Documents;

public sealed class UserDocument : CosmosDocument
{
    public required string Email { get; init; }
    public string? PasswordHash { get; init; }
    public bool EmailConfirmed { get; init; }
    public List<UserRole> Roles { get; init; } = [];
    public Profile? Profile { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}
