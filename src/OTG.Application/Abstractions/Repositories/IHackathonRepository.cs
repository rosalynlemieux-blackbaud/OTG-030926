using OTG.Domain.Hackathons;

namespace OTG.Application.Abstractions.Repositories;

public interface IHackathonRepository
{
    Task<Hackathon?> GetByIdAsync(string hackathonId, CancellationToken cancellationToken = default);
    Task UpsertAsync(Hackathon hackathon, CancellationToken cancellationToken = default);
}
