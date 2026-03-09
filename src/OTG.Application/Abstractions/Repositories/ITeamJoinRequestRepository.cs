using OTG.Domain.Teams;

namespace OTG.Application.Abstractions.Repositories;

public interface ITeamJoinRequestRepository
{
    Task<IReadOnlyList<TeamJoinRequest>> GetByTeamAsync(string teamId, CancellationToken cancellationToken = default);
    Task UpsertAsync(TeamJoinRequest request, CancellationToken cancellationToken = default);
}
