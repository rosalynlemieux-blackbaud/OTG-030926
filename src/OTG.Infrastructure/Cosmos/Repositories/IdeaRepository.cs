using Microsoft.Azure.Cosmos;
using OTG.Application.Abstractions.Repositories;
using OTG.Domain.Ideas;
using OTG.Infrastructure.Cosmos.Documents;
using OTG.Infrastructure.Cosmos.Mapping;

namespace OTG.Infrastructure.Cosmos.Repositories;

internal sealed class IdeaRepository(ICosmosContainerProvider containerProvider) : IIdeaRepository
{
    public async Task<Idea?> GetByIdAsync(string id, string hackathonId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await containerProvider.Ideas.ReadItemAsync<IdeaDocument>(id, new PartitionKey(hackathonId), cancellationToken: cancellationToken);
            return response.Resource.ToDomain();
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<Idea>> SearchAsync(string hackathonId, IdeaStatus? status, string? trackId, string? searchText, CancellationToken cancellationToken = default)
    {
        var queryText = "SELECT * FROM c WHERE c.hackathonId = @hackathonId";

        if (status.HasValue)
        {
            queryText += " AND c.status = @status";
        }

        if (!string.IsNullOrWhiteSpace(trackId))
        {
            queryText += " AND c.trackId = @trackId";
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            queryText += " AND (CONTAINS(c.title, @searchText, true) OR CONTAINS(c.description, @searchText, true))";
        }

        var query = new QueryDefinition(queryText).WithParameter("@hackathonId", hackathonId);

        if (status.HasValue)
        {
            query.WithParameter("@status", status.Value);
        }

        if (!string.IsNullOrWhiteSpace(trackId))
        {
            query.WithParameter("@trackId", trackId);
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            query.WithParameter("@searchText", searchText);
        }

        var iterator = containerProvider.Ideas.GetItemQueryIterator<IdeaDocument>(
            query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(hackathonId)
            });

        var output = new List<Idea>();
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            output.AddRange(page.Resource.Select(item => item.ToDomain()));
        }

        return output;
    }

    public Task UpsertAsync(Idea idea, CancellationToken cancellationToken = default)
    {
        return containerProvider.Ideas.UpsertItemAsync(idea.ToDocument(), new PartitionKey(idea.HackathonId), cancellationToken: cancellationToken);
    }
}
