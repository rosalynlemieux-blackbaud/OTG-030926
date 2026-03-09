namespace OTG.Api.Contracts;

public sealed class UpdateProfileRequest
{
    public string? Name { get; init; }
    public string? AvatarUrl { get; init; }
    public string? Department { get; init; }
    public string? Location { get; init; }
    public IReadOnlyList<string> Skills { get; init; } = [];
    public IReadOnlyList<string> Interests { get; init; } = [];
}
