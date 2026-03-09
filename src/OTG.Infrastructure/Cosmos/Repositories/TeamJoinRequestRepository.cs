using Microsoft.Azure.Cosmos;
using OTG.Application.Abstractions.Repositories;
using OTG.Domain.Teams;
using OTG.Infrastructure.Cosmos.Documents;
using OTG.Infrastructure.Cosmos.Mapping;

namespace OTG.Infrastructure.Cosmos.Repositories;

internal sealed class TeamJoinRequestRepository(ICosmosContainerProvider containerProvider) : ITeamJoinRequestRepository
{
    public async Task<IReadOnlyList<TeamJoinRequest>> GetByTeamAsync(string teamId, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.teamId = @teamId").WithParameter("@teamId", teamId);
        var iterator = containerProvider.TeamJoinRequests.GetItemQueryIterator<TeamJoinRequestDocument>(query, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(teamId) });

        var output = new List<TeamJoinRequest>();
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            output.AddRange(page.Resource.Select(item => item.ToDomain()));
        }

        return output;
    }

    public Task UpsertAsync(TeamJoinRequest request, CancellationToken cancellationToken = default)
    {
        return containerProvider.TeamJoinRequests.UpsertItemAsync(request.ToDocument(), new PartitionKey(request.TeamId), cancellationToken: cancellationToken);
    }
}
