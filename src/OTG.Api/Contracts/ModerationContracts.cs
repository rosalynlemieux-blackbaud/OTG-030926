namespace OTG.Api.Contracts;

public sealed class ModerateContentRequest
{
    public bool IsModerated { get; init; }
    public string? Reason { get; init; }
}
