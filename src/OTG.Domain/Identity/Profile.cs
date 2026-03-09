using OTG.Domain.Common;

namespace OTG.Domain.Identity;

public sealed class Profile : AuditableEntity
{
    public required string UserId { get; init; }
    public string? Name { get; set; }
    public required string Email { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Department { get; set; }
    public string? Location { get; set; }
    public List<string> Skills { get; set; } = [];
    public List<string> Interests { get; set; } = [];
    public bool Banned { get; set; }
    public DateTimeOffset? BannedAtUtc { get; set; }

    public string? BlackbaudId { get; set; }
    public bool BlackbaudLinked { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Title { get; set; }
    public string? Phone { get; set; }
    public string? JobTitle { get; set; }
    public string? Organization { get; set; }
    public DateTime? Birthdate { get; set; }
    public string? EnvironmentId { get; set; }
    public string? EnvironmentName { get; set; }
    public string? LegalEntityId { get; set; }
    public string? LegalEntityName { get; set; }
    public string? BlackbaudRefreshToken { get; set; }
    public DateTimeOffset? BlackbaudRefreshTokenUpdatedAtUtc { get; set; }
    public DateTimeOffset? BlackbaudAccessTokenExpiresAtUtc { get; set; }
    public List<MerchantAccount> MerchantAccounts { get; set; } = [];
}
