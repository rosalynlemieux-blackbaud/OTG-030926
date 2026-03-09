namespace OTG.Api.Contracts;

public sealed class UpdateLeaderboardBandsRequest
{
    public decimal PlatinumMinPercentile { get; init; } = 90m;
    public decimal GoldMinPercentile { get; init; } = 75m;
    public decimal SilverMinPercentile { get; init; } = 50m;
}
