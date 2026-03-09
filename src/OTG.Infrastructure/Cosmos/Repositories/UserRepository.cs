using Microsoft.Azure.Cosmos;
using OTG.Application.Abstractions.Repositories;
using OTG.Infrastructure.Cosmos.Mapping;
using DomainUser = OTG.Domain.Identity.User;

namespace OTG.Infrastructure.Cosmos.Repositories;

internal sealed class UserRepository(ICosmosContainerProvider containerProvider) : IUserRepository
{
    public async Task<DomainUser?> GetByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await containerProvider.Users.ReadItemAsync<Documents.UserDocument>(userId, new PartitionKey(userId), cancellationToken: cancellationToken);
            return response.Resource.ToDomain();
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<DomainUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.email = @email").WithParameter("@email", email);
        var iterator = containerProvider.Users.GetItemQueryIterator<Documents.UserDocument>(query);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            var user = page.Resource.FirstOrDefault();
            if (user is not null)
            {
                return user.ToDomain();
            }
        }

        return null;
    }

    public async Task<IReadOnlyList<DomainUser>> SearchAsync(string? query, int limit = 50, CancellationToken cancellationToken = default)
    {
        var cappedLimit = Math.Clamp(limit, 1, 100);
        var normalized = query?.Trim();
        QueryDefinition definition;

        if (string.IsNullOrWhiteSpace(normalized))
        {
            definition = new QueryDefinition("SELECT * FROM c");
        }
        else
        {
            definition = new QueryDefinition(
                    "SELECT * FROM c WHERE CONTAINS(c.email, @query, true) OR CONTAINS(c.profile.name, @query, true) OR CONTAINS(c.profile.department, @query, true)")
                .WithParameter("@query", normalized);
        }

        var iterator = containerProvider.Users.GetItemQueryIterator<Documents.UserDocument>(definition, requestOptions: new QueryRequestOptions { MaxItemCount = cappedLimit });
        var output = new List<DomainUser>();
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            foreach (var item in page.Resource)
            {
                output.Add(item.ToDomain());
                if (output.Count >= cappedLimit)
                {
                    return output;
                }
            }
        }

        return output;
    }

    public Task UpsertAsync(DomainUser user, CancellationToken cancellationToken = default)
    {
        return containerProvider.Users.UpsertItemAsync(user.ToDocument(), new PartitionKey(user.Id), cancellationToken: cancellationToken);
    }
}
