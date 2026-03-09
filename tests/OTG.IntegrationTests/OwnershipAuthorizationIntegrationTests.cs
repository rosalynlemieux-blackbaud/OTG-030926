using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OTG.Application.Abstractions.Repositories;
using OTG.Domain.Identity;
using OTG.Domain.Ideas;
using OTG.Domain.Teams;

namespace OTG.IntegrationTests;

public sealed class OwnershipAuthorizationIntegrationTests
{
    [Fact]
    public async Task UpdateIdea_ReturnsForbidden_ForNonOwnerParticipant()
    {
        await using var factory = new TestApiFactory();
        var owner = factory.SeedUser("idea-owner@example.com", UserRole.Participant);
        var other = factory.SeedUser("idea-other@example.com", UserRole.Participant);
        var idea = factory.SeedIdea("hack-own-idea", "idea-own-1", owner.Id);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, other.Id, other.Email, "Participant");

        var response = await client.PostAsJsonAsync("/api/ideas", new
        {
            Id = idea.Id,
            HackathonId = idea.HackathonId,
            Title = "Updated by non-owner",
            Description = "desc",
            Status = IdeaStatus.Draft,
            TermsAccepted = true
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateIdea_Succeeds_ForOwner_AndAdmin()
    {
        await using var factory = new TestApiFactory();
        var owner = factory.SeedUser("idea-owner-ok@example.com", UserRole.Participant);
        var admin = factory.SeedUser("idea-admin@example.com", UserRole.Admin);
        var idea = factory.SeedIdea("hack-own-idea-ok", "idea-own-2", owner.Id);

        var ownerClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(ownerClient, owner.Id, owner.Email, "Participant");
        var ownerResponse = await ownerClient.PostAsJsonAsync("/api/ideas", new
        {
            Id = idea.Id,
            HackathonId = idea.HackathonId,
            Title = "Owner updated title",
            Description = "desc",
            Status = IdeaStatus.Submitted,
            TermsAccepted = true
        });
        Assert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode);

        var adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(adminClient, admin.Id, admin.Email, "Admin");
        var adminResponse = await adminClient.PostAsJsonAsync("/api/ideas", new
        {
            Id = idea.Id,
            HackathonId = idea.HackathonId,
            Title = "Admin updated title",
            Description = "desc",
            Status = IdeaStatus.Draft,
            TermsAccepted = true
        });
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateTeam_ReturnsForbidden_ForNonLeaderParticipant()
    {
        await using var factory = new TestApiFactory();
        var leader = factory.SeedUser("team-leader@example.com", UserRole.Participant);
        var other = factory.SeedUser("team-other@example.com", UserRole.Participant);
        var team = factory.SeedTeam("hack-own-team", "team-own-1", leader.Id);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, other.Id, other.Email, "Participant");

        var response = await client.PostAsJsonAsync("/api/teams", new
        {
            Id = team.Id,
            HackathonId = team.HackathonId,
            Name = "Updated by non-leader",
            MaxSize = 5
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTeam_Succeeds_ForLeader_AndAdmin()
    {
        await using var factory = new TestApiFactory();
        var leader = factory.SeedUser("team-leader-ok@example.com", UserRole.Participant);
        var admin = factory.SeedUser("team-admin@example.com", UserRole.Admin);
        var team = factory.SeedTeam("hack-own-team-ok", "team-own-2", leader.Id);

        var leaderClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(leaderClient, leader.Id, leader.Email, "Participant");
        var leaderResponse = await leaderClient.PostAsJsonAsync("/api/teams", new
        {
            Id = team.Id,
            HackathonId = team.HackathonId,
            Name = "Leader updated name",
            MaxSize = 6
        });
        Assert.Equal(HttpStatusCode.OK, leaderResponse.StatusCode);

        var adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(adminClient, admin.Id, admin.Email, "Admin");
        var adminResponse = await adminClient.PostAsJsonAsync("/api/teams", new
        {
            Id = team.Id,
            HackathonId = team.HackathonId,
            Name = "Admin updated name",
            MaxSize = 7
        });
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
    }

    private static void AddAuthHeaders(HttpClient client, string userId, string email, string role)
    {
        client.DefaultRequestHeaders.Remove(TestAuthHandler.UserIdHeader);
        client.DefaultRequestHeaders.Remove(TestAuthHandler.EmailHeader);
        client.DefaultRequestHeaders.Remove(TestAuthHandler.RolesHeader);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, email);
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
    }

    private sealed class TestApiFactory : WebApplicationFactory<OTG.Api.Program>
    {
        public InMemoryUserRepository UserRepository { get; } = new();
        public InMemoryIdeaRepository IdeaRepository { get; } = new();
        public InMemoryTeamRepository TeamRepository { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IUserRepository>();
                services.RemoveAll<IIdeaRepository>();
                services.RemoveAll<ITeamRepository>();

                services
                    .AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ =>
                    {
                    });

                services.AddSingleton<IUserRepository>(UserRepository);
                services.AddSingleton<IIdeaRepository>(IdeaRepository);
                services.AddSingleton<ITeamRepository>(TeamRepository);
            });
        }

        public User SeedUser(string email, params UserRole[] roles)
        {
            var id = Guid.NewGuid().ToString("N");
            var user = new User
            {
                Id = id,
                Email = email,
                EmailConfirmed = true,
                Roles = roles.Any() ? roles.ToList() : [UserRole.Participant],
                Profile = new Profile
                {
                    Id = Guid.NewGuid().ToString("N"),
                    UserId = id,
                    Email = email,
                    Banned = false
                }
            };

            UserRepository.UpsertAsync(user).GetAwaiter().GetResult();
            return user;
        }

        public Idea SeedIdea(string hackathonId, string ideaId, string authorId)
        {
            var idea = new Idea
            {
                Id = ideaId,
                HackathonId = hackathonId,
                AuthorId = authorId,
                Title = ideaId,
                Description = "desc",
                Status = IdeaStatus.Draft,
                TermsAccepted = true
            };

            IdeaRepository.UpsertAsync(idea).GetAwaiter().GetResult();
            return idea;
        }

        public Team SeedTeam(string hackathonId, string teamId, string leaderId)
        {
            var team = new Team
            {
                Id = teamId,
                HackathonId = hackathonId,
                Name = teamId,
                LeaderId = leaderId,
                MaxSize = 5,
                Members = [new TeamMember { UserId = leaderId }]
            };

            TeamRepository.UpsertAsync(team).GetAwaiter().GetResult();
            return team;
        }
    }

    private sealed class InMemoryUserRepository : IUserRepository
    {
        private readonly Dictionary<string, User> byId = [];

        public Task<User?> GetByIdAsync(string userId, CancellationToken cancellationToken = default)
            => Task.FromResult(byId.TryGetValue(userId, out var user) ? Clone(user) : null);

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            var user = byId.Values.FirstOrDefault(item => string.Equals(item.Email, email, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(user is null ? null : Clone(user));
        }

        public Task<IReadOnlyList<User>> SearchAsync(string? query, int limit = 50, CancellationToken cancellationToken = default)
        {
            IEnumerable<User> users = byId.Values;
            if (!string.IsNullOrWhiteSpace(query))
            {
                users = users.Where(user =>
                    user.Email.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || (user.Profile?.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (user.Profile?.Department?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            return Task.FromResult<IReadOnlyList<User>>(users.Take(Math.Clamp(limit, 1, 100)).Select(Clone).ToList());
        }

        public Task UpsertAsync(User user, CancellationToken cancellationToken = default)
        {
            byId[user.Id] = Clone(user);
            return Task.CompletedTask;
        }

        private static User Clone(User user)
        {
            return new User
            {
                Id = user.Id,
                Email = user.Email,
                PasswordHash = user.PasswordHash,
                EmailConfirmed = user.EmailConfirmed,
                Roles = user.Roles.ToList(),
                Profile = user.Profile is null
                    ? null
                    : new Profile
                    {
                        Id = user.Profile.Id,
                        UserId = user.Profile.UserId,
                        Email = user.Profile.Email,
                        Name = user.Profile.Name,
                        Department = user.Profile.Department,
                        Banned = user.Profile.Banned,
                        BannedAtUtc = user.Profile.BannedAtUtc,
                        CreatedAtUtc = user.Profile.CreatedAtUtc,
                        UpdatedAtUtc = user.Profile.UpdatedAtUtc
                    },
                CreatedAtUtc = user.CreatedAtUtc,
                UpdatedAtUtc = user.UpdatedAtUtc
            };
        }
    }

    private sealed class InMemoryIdeaRepository : IIdeaRepository
    {
        private readonly Dictionary<(string HackathonId, string IdeaId), Idea> byId = [];

        public Task<Idea?> GetByIdAsync(string id, string hackathonId, CancellationToken cancellationToken = default)
            => Task.FromResult(byId.TryGetValue((hackathonId, id), out var idea) ? Clone(idea) : null);

        public Task<IReadOnlyList<Idea>> SearchAsync(string hackathonId, IdeaStatus? status, string? trackId, string? searchText, CancellationToken cancellationToken = default)
        {
            IEnumerable<Idea> ideas = byId.Values.Where(idea => string.Equals(idea.HackathonId, hackathonId, StringComparison.Ordinal));
            if (status.HasValue)
            {
                ideas = ideas.Where(idea => idea.Status == status.Value);
            }

            if (!string.IsNullOrWhiteSpace(trackId))
            {
                ideas = ideas.Where(idea => string.Equals(idea.TrackId, trackId, StringComparison.Ordinal));
            }

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                ideas = ideas.Where(idea => idea.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                    || idea.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase));
            }

            return Task.FromResult<IReadOnlyList<Idea>>(ideas.Select(Clone).ToList());
        }

        public Task UpsertAsync(Idea idea, CancellationToken cancellationToken = default)
        {
            byId[(idea.HackathonId, idea.Id)] = Clone(idea);
            return Task.CompletedTask;
        }

        private static Idea Clone(Idea idea)
        {
            return new Idea
            {
                Id = idea.Id,
                HackathonId = idea.HackathonId,
                AuthorId = idea.AuthorId,
                Title = idea.Title,
                Description = idea.Description,
                Status = idea.Status,
                TrackId = idea.TrackId,
                TeamId = idea.TeamId,
                AwardIds = idea.AwardIds.ToList(),
                AssignedJudgeIds = idea.AssignedJudgeIds.ToList(),
                Tags = idea.Tags.ToList(),
                Attachments = idea.Attachments.ToList(),
                TermsAccepted = idea.TermsAccepted,
                Votes = idea.Votes,
                VideoUrl = idea.VideoUrl,
                RepoUrl = idea.RepoUrl,
                DemoUrl = idea.DemoUrl,
                SubmittedAtUtc = idea.SubmittedAtUtc,
                CreatedAtUtc = idea.CreatedAtUtc,
                UpdatedAtUtc = idea.UpdatedAtUtc
            };
        }
    }

    private sealed class InMemoryTeamRepository : ITeamRepository
    {
        private readonly Dictionary<(string TeamId, string HackathonId), Team> byId = [];

        public Task<Team?> GetByIdAsync(string teamId, string hackathonId, CancellationToken cancellationToken = default)
            => Task.FromResult(byId.TryGetValue((teamId, hackathonId), out var team) ? Clone(team) : null);

        public Task<IReadOnlyList<Team>> SearchAsync(string hackathonId, string? query, CancellationToken cancellationToken = default)
        {
            var teams = byId.Values.Where(team => string.Equals(team.HackathonId, hackathonId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(query))
            {
                teams = teams.Where(team => team.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
            }

            return Task.FromResult<IReadOnlyList<Team>>(teams.Select(Clone).ToList());
        }

        public Task UpsertAsync(Team team, CancellationToken cancellationToken = default)
        {
            byId[(team.Id, team.HackathonId)] = Clone(team);
            return Task.CompletedTask;
        }

        private static Team Clone(Team team)
        {
            return new Team
            {
                Id = team.Id,
                HackathonId = team.HackathonId,
                Name = team.Name,
                Description = team.Description,
                ImageUrl = team.ImageUrl,
                LeaderId = team.LeaderId,
                MaxSize = team.MaxSize,
                Skills = team.Skills.ToList(),
                Members = team.Members.Select(member => new TeamMember
                {
                    UserId = member.UserId,
                    JoinedAtUtc = member.JoinedAtUtc
                }).ToList(),
                CreatedAtUtc = team.CreatedAtUtc,
                UpdatedAtUtc = team.UpdatedAtUtc
            };
        }
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";
        public const string UserIdHeader = "X-Test-UserId";
        public const string EmailHeader = "X-Test-Email";
        public const string RolesHeader = "X-Test-Roles";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(UserIdHeader, out var userId) || string.IsNullOrWhiteSpace(userId))
            {
                return Task.FromResult(AuthenticateResult.Fail("Missing user id"));
            }

            var email = Request.Headers.TryGetValue(EmailHeader, out var emailValues)
                ? emailValues.ToString()
                : "test@example.com";

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(ClaimTypes.Email, email)
            };

            if (Request.Headers.TryGetValue(RolesHeader, out var rolesValues))
            {
                foreach (var role in rolesValues.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }
            }

            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
