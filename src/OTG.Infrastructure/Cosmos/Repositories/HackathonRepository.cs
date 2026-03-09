using Microsoft.Azure.Cosmos;
using OTG.Application.Abstractions.Repositories;
using OTG.Domain.Hackathons;
using OTG.Infrastructure.Cosmos.Documents;
using OTG.Infrastructure.Cosmos.Mapping;

namespace OTG.Infrastructure.Cosmos.Repositories;

internal sealed class HackathonRepository(ICosmosContainerProvider containerProvider) : IHackathonRepository
{
    public async Task<Hackathon?> GetByIdAsync(string hackathonId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await containerProvider.Hackathons.ReadItemAsync<HackathonDocument>(hackathonId, new PartitionKey(hackathonId), cancellationToken: cancellationToken);
            return response.Resource.ToDomain();
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task UpsertAsync(Hackathon hackathon, CancellationToken cancellationToken = default)
    {
        return containerProvider.Hackathons.UpsertItemAsync(hackathon.ToDocument(), new PartitionKey(hackathon.HackathonId), cancellationToken: cancellationToken);
    }
}
