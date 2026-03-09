using OTG.Domain.Teams;

namespace OTG.Application.Abstractions.Repositories;

public interface ITeamInviteRepository
{
    Task<IReadOnlyList<TeamInvite>> GetByTeamAsync(string teamId, CancellationToken cancellationToken = default);
    Task<TeamInvite?> GetByTokenAsync(string token, string teamId, CancellationToken cancellationToken = default);
    Task UpsertAsync(TeamInvite invite, CancellationToken cancellationToken = default);
}
