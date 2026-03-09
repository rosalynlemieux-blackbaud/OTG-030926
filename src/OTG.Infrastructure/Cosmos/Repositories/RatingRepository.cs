using Microsoft.Azure.Cosmos;
using OTG.Application.Abstractions.Repositories;
using OTG.Domain.Ideas;
using OTG.Infrastructure.Cosmos.Documents;
using OTG.Infrastructure.Cosmos.Mapping;

namespace OTG.Infrastructure.Cosmos.Repositories;

internal sealed class RatingRepository(ICosmosContainerProvider containerProvider) : IRatingRepository
{
    public async Task<IReadOnlyList<IdeaRating>> GetByIdeaAsync(string ideaId, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.ideaId = @ideaId").WithParameter("@ideaId", ideaId);
        var iterator = containerProvider.Ratings.GetItemQueryIterator<RatingDocument>(query, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(ideaId) });

        var output = new List<IdeaRating>();
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            output.AddRange(page.Resource.Select(item => item.ToDomain()));
        }

        return output;
    }

    public Task UpsertAsync(IdeaRating rating, CancellationToken cancellationToken = default)
    {
        return containerProvider.Ratings.UpsertItemAsync(rating.ToDocument(), new PartitionKey(rating.IdeaId), cancellationToken: cancellationToken);
    }
}
