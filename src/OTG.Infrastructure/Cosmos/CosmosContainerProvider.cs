using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace OTG.Infrastructure.Cosmos;

internal sealed class CosmosContainerProvider : ICosmosContainerProvider
{
    public CosmosContainerProvider(CosmosClient cosmosClient, IOptions<CosmosOptions> options)
    {
        var configured = options.Value;
        var database = cosmosClient.GetDatabase(configured.DatabaseName);

        Users = database.GetContainer(configured.Containers.Users);
        Hackathons = database.GetContainer(configured.Containers.Hackathons);
        Ideas = database.GetContainer(configured.Containers.Ideas);
        Comments = database.GetContainer(configured.Containers.Comments);
        Ratings = database.GetContainer(configured.Containers.Ratings);
        Teams = database.GetContainer(configured.Containers.Teams);
        TeamJoinRequests = database.GetContainer(configured.Containers.TeamJoinRequests);
        TeamInvites = database.GetContainer(configured.Containers.TeamInvites);
    }

    public Container Users { get; }
    public Container Hackathons { get; }
    public Container Ideas { get; }
    public Container Comments { get; }
    public Container Ratings { get; }
    public Container Teams { get; }
    public Container TeamJoinRequests { get; }
    public Container TeamInvites { get; }
}
