using Microsoft.Azure.Cosmos;
using OTG.Application.Abstractions.Repositories;
using OTG.Domain.Teams;
using OTG.Infrastructure.Cosmos.Documents;
using OTG.Infrastructure.Cosmos.Mapping;

namespace OTG.Infrastructure.Cosmos.Repositories;

internal sealed class TeamInviteRepository(ICosmosContainerProvider containerProvider) : ITeamInviteRepository
{
    public async Task<IReadOnlyList<TeamInvite>> GetByTeamAsync(string teamId, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.teamId = @teamId").WithParameter("@teamId", teamId);
        var iterator = containerProvider.TeamInvites.GetItemQueryIterator<TeamInviteDocument>(query, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(teamId) });

        var output = new List<TeamInvite>();
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            output.AddRange(page.Resource.Select(item => item.ToDomain()));
        }

        return output;
    }

    public async Task<TeamInvite?> GetByTokenAsync(string token, string teamId, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.teamId = @teamId AND c.token = @token")
            .WithParameter("@teamId", teamId)
            .WithParameter("@token", token);

        var iterator = containerProvider.TeamInvites.GetItemQueryIterator<TeamInviteDocument>(query, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(teamId) });
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            var invite = page.Resource.FirstOrDefault();
            if (invite is not null)
            {
                return invite.ToDomain();
            }
        }

        return null;
    }

    public Task UpsertAsync(TeamInvite invite, CancellationToken cancellationToken = default)
    {
        return containerProvider.TeamInvites.UpsertItemAsync(invite.ToDocument(), new PartitionKey(invite.TeamId), cancellationToken: cancellationToken);
    }
}
