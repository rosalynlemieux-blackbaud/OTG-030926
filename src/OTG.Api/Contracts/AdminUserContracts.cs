namespace OTG.Api.Contracts;

public sealed class UpdateUserRolesRequest
{
    public required IReadOnlyList<string> Roles { get; init; }
}

public sealed class SetUserBanRequest
{
    public required bool Banned { get; init; }
}
