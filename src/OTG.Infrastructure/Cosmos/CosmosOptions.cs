namespace OTG.Infrastructure.Cosmos;

public sealed class CosmosOptions
{
    public const string SectionName = "Cosmos";

    public string AccountEndpoint { get; init; } = string.Empty;
    public string AccountKey { get; init; } = string.Empty;
    public string DatabaseName { get; init; } = "otg-dev";
    public CosmosContainerOptions Containers { get; init; } = new();
}

public sealed class CosmosContainerOptions
{
    public string Users { get; init; } = "users";
    public string Hackathons { get; init; } = "hackathons";
    public string Ideas { get; init; } = "ideas";
    public string Comments { get; init; } = "comments";
    public string Ratings { get; init; } = "ratings";
    public string Teams { get; init; } = "teams";
    public string TeamJoinRequests { get; init; } = "teamJoinRequests";
    public string TeamInvites { get; init; } = "teamInvites";
}
