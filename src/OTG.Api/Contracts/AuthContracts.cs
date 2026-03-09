namespace OTG.Api.Contracts;

public sealed class RegisterRequest
{
    public required string Email { get; init; }
    public required string Password { get; init; }
}

public sealed class LoginRequest
{
    public required string Email { get; init; }
    public required string Password { get; init; }
}

public sealed class AuthResponse
{
    public required string AccessToken { get; init; }
    public required string UserId { get; init; }
    public required string Email { get; init; }
    public required IReadOnlyList<string> Roles { get; init; }
}

public sealed class BlackbaudStartResponse
{
    public required string AuthorizationUrl { get; init; }
    public required string State { get; init; }
}

public sealed class BlackbaudCallbackRequest
{
    public string? Code { get; init; }
    public string? State { get; init; }
}
