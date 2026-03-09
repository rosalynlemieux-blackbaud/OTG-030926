using OTG.Domain.Ideas;

namespace OTG.Application.Abstractions.Repositories;

public interface ICommentRepository
{
    Task<IReadOnlyList<Comment>> GetByIdeaAsync(string ideaId, CancellationToken cancellationToken = default);
    Task UpsertAsync(Comment comment, CancellationToken cancellationToken = default);
}
