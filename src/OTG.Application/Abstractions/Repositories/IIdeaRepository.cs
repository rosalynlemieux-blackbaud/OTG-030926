using OTG.Domain.Ideas;

namespace OTG.Application.Abstractions.Repositories;

public interface IIdeaRepository
{
    Task<Idea?> GetByIdAsync(string id, string hackathonId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Idea>> SearchAsync(string hackathonId, IdeaStatus? status, string? trackId, string? searchText, CancellationToken cancellationToken = default);
    Task UpsertAsync(Idea idea, CancellationToken cancellationToken = default);
}
