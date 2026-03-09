using Microsoft.Azure.Cosmos;
using OTG.Application.Abstractions.Repositories;
using OTG.Domain.Teams;
using OTG.Infrastructure.Cosmos.Documents;
using OTG.Infrastructure.Cosmos.Mapping;

namespace OTG.Infrastructure.Cosmos.Repositories;

internal sealed class TeamRepository(ICosmosContainerProvider containerProvider) : ITeamRepository
{
    public async Task<Team?> GetByIdAsync(string teamId, string hackathonId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await containerProvider.Teams.ReadItemAsync<TeamDocument>(teamId, new PartitionKey(hackathonId), cancellationToken: cancellationToken);
            return response.Resource.ToDomain();
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<Team>> SearchAsync(string hackathonId, string? query, CancellationToken cancellationToken = default)
    {
        var queryText = "SELECT * FROM c WHERE c.hackathonId = @hackathonId";
        if (!string.IsNullOrWhiteSpace(query))
        {
            queryText += " AND (CONTAINS(c.name, @query, true) OR CONTAINS(c.description, @query, true))";
        }

        var iterator = containerProvider.Teams.GetItemQueryIterator<TeamDocument>(
            new QueryDefinition(queryText)
                .WithParameter("@hackathonId", hackathonId)
                .WithParameter("@query", query),
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(hackathonId) });

        var output = new List<Team>();
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            output.AddRange(page.Resource.Select(item => item.ToDomain()));
        }

        return output;
    }

    public Task UpsertAsync(Team team, CancellationToken cancellationToken = default)
    {
        return containerProvider.Teams.UpsertItemAsync(team.ToDocument(), new PartitionKey(team.HackathonId), cancellationToken: cancellationToken);
    }
}
