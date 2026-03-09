namespace OTG.Api.Options;

public sealed class BlackbaudOptions
{
    public const string SectionName = "Blackbaud";

    public string ApplicationId { get; init; } = string.Empty;
    public string ApplicationSecret { get; init; } = string.Empty;
    public string SubscriptionKey { get; init; } = string.Empty;
    public string PaymentsSubscriptionKey { get; init; } = string.Empty;
    public string RedirectUri { get; init; } = string.Empty;
    public List<string> AllowedOrigins { get; init; } = [];
}
