namespace OTG.Api.Services;

public interface IBlackbaudOAuthService
{
    Task<BlackbaudUserData> ExchangeCodeForUserAsync(string code, CancellationToken cancellationToken);
    Task<BlackbaudTokenRefreshResult> RefreshAccessTokenAsync(string refreshToken, CancellationToken cancellationToken);
}

public sealed class BlackbaudUserData
{
    public required string Email { get; init; }
    public string? BlackbaudId { get; init; }
    public string? FullName { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Title { get; init; }
    public string? JobTitle { get; init; }
    public string? Organization { get; init; }
    public string? Phone { get; init; }
    public DateTime? Birthdate { get; init; }
    public string? EnvironmentId { get; init; }
    public string? EnvironmentName { get; init; }
    public string? LegalEntityId { get; init; }
    public string? LegalEntityName { get; init; }
    public string? RefreshToken { get; init; }
    public DateTimeOffset? AccessTokenExpiresAtUtc { get; init; }
    public IReadOnlyList<BlackbaudMerchantAccountData> MerchantAccounts { get; init; } = [];
}

public sealed class BlackbaudTokenRefreshResult
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public DateTimeOffset? AccessTokenExpiresAtUtc { get; init; }
}

public sealed class BlackbaudMerchantAccountData
{
    public required string Name { get; init; }
    public required string MerchantId { get; init; }
    public string? Currency { get; init; }
    public string? ProcessMode { get; init; }
    public bool Active { get; init; }
}
