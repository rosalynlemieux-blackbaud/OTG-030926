using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace OTG.Infrastructure.Cosmos;

public sealed class CosmosBootstrapHostedService(
    CosmosClient cosmosClient,
    IOptions<CosmosOptions> options) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var configured = options.Value;
        var database = await cosmosClient.CreateDatabaseIfNotExistsAsync(configured.DatabaseName, cancellationToken: cancellationToken);

        await database.Database.CreateContainerIfNotExistsAsync(configured.Containers.Users, "/id", cancellationToken: cancellationToken);
        await database.Database.CreateContainerIfNotExistsAsync(configured.Containers.Hackathons, "/hackathonId", cancellationToken: cancellationToken);
        await database.Database.CreateContainerIfNotExistsAsync(configured.Containers.Ideas, "/hackathonId", cancellationToken: cancellationToken);
        await database.Database.CreateContainerIfNotExistsAsync(configured.Containers.Comments, "/ideaId", cancellationToken: cancellationToken);
        await database.Database.CreateContainerIfNotExistsAsync(configured.Containers.Ratings, "/ideaId", cancellationToken: cancellationToken);
        await database.Database.CreateContainerIfNotExistsAsync(configured.Containers.Teams, "/hackathonId", cancellationToken: cancellationToken);
        await database.Database.CreateContainerIfNotExistsAsync(configured.Containers.TeamJoinRequests, "/teamId", cancellationToken: cancellationToken);
        await database.Database.CreateContainerIfNotExistsAsync(configured.Containers.TeamInvites, "/teamId", cancellationToken: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
