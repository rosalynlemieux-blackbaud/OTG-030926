using Microsoft.Azure.Cosmos;
using OTG.Application.Abstractions.Repositories;
using OTG.Domain.Ideas;
using OTG.Infrastructure.Cosmos.Documents;
using OTG.Infrastructure.Cosmos.Mapping;

namespace OTG.Infrastructure.Cosmos.Repositories;

internal sealed class CommentRepository(ICosmosContainerProvider containerProvider) : ICommentRepository
{
    public async Task<IReadOnlyList<Comment>> GetByIdeaAsync(string ideaId, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.ideaId = @ideaId").WithParameter("@ideaId", ideaId);
        var iterator = containerProvider.Comments.GetItemQueryIterator<CommentDocument>(query, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(ideaId) });

        var output = new List<Comment>();
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            output.AddRange(page.Resource.Select(item => item.ToDomain()));
        }

        return output;
    }

    public Task UpsertAsync(Comment comment, CancellationToken cancellationToken = default)
    {
        return containerProvider.Comments.UpsertItemAsync(comment.ToDocument(), new PartitionKey(comment.IdeaId), cancellationToken: cancellationToken);
    }
}
