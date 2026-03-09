using OTG.Domain.Teams;

namespace OTG.Application.Abstractions.Repositories;

public interface ITeamRepository
{
    Task<Team?> GetByIdAsync(string teamId, string hackathonId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Team>> SearchAsync(string hackathonId, string? query, CancellationToken cancellationToken = default);
    Task UpsertAsync(Team team, CancellationToken cancellationToken = default);
}
