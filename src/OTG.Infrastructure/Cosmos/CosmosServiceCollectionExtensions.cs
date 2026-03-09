using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OTG.Application.Abstractions.Repositories;
using OTG.Infrastructure.Cosmos.Repositories;

namespace OTG.Infrastructure.Cosmos;

public static class CosmosServiceCollectionExtensions
{
    public static IServiceCollection AddOtgCosmos(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CosmosOptions>(configuration.GetSection(CosmosOptions.SectionName));

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<CosmosOptions>>().Value;
            var clientOptions = new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                }
            };

            return new CosmosClient(options.AccountEndpoint, options.AccountKey, clientOptions);
        });

        services.AddSingleton<ICosmosContainerProvider, CosmosContainerProvider>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IHackathonRepository, HackathonRepository>();
        services.AddScoped<IIdeaRepository, IdeaRepository>();
        services.AddScoped<ICommentRepository, CommentRepository>();
        services.AddScoped<IRatingRepository, RatingRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<ITeamJoinRequestRepository, TeamJoinRequestRepository>();
        services.AddScoped<ITeamInviteRepository, TeamInviteRepository>();

        return services;
    }
}
