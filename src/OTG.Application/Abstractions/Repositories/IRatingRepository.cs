using OTG.Domain.Ideas;

namespace OTG.Application.Abstractions.Repositories;

public interface IRatingRepository
{
    Task<IReadOnlyList<IdeaRating>> GetByIdeaAsync(string ideaId, CancellationToken cancellationToken = default);
    Task UpsertAsync(IdeaRating rating, CancellationToken cancellationToken = default);
}
