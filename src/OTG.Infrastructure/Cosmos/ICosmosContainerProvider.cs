using Microsoft.Azure.Cosmos;

namespace OTG.Infrastructure.Cosmos;

public interface ICosmosContainerProvider
{
    Container Users { get; }
    Container Hackathons { get; }
    Container Ideas { get; }
    Container Comments { get; }
    Container Ratings { get; }
    Container Teams { get; }
    Container TeamJoinRequests { get; }
    Container TeamInvites { get; }
}
