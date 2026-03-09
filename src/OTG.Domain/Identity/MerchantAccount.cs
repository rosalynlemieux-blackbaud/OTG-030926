namespace OTG.Domain.Identity;

public sealed class MerchantAccount
{
    public required string Name { get; init; }
    public required string MerchantId { get; init; }
    public string? Currency { get; init; }
    public string? ProcessMode { get; init; }
    public bool Active { get; init; }
}
