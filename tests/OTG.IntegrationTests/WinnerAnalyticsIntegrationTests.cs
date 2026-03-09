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
using OTG.Domain.Hackathons;
using OTG.Domain.Identity;
using OTG.Domain.Ideas;
using OTG.Domain.Teams;

namespace OTG.IntegrationTests;

public sealed class WinnerAnalyticsIntegrationTests
{
    [Fact]
    public async Task Winners_ReturnsOnlyWinningIdeas_GroupedByTrack()
    {
        await using var factory = new TestApiFactory();
        var user = factory.SeedUser("participant1@example.com", UserRole.Participant);
        var hackathon = factory.SeedHackathon("h-w1", [new Track { Id = "track-1", Name = "Impact" }]);

        factory.SeedIdea(hackathon.HackathonId, "idea-winner", IdeaStatus.Winner, trackId: "track-1");
        factory.SeedIdea(hackathon.HackathonId, "idea-draft", IdeaStatus.Draft, trackId: "track-1");
        factory.SeedIdea(hackathon.HackathonId, "idea-special", IdeaStatus.Winner, trackId: null);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, user.Id, user.Email, "Participant");

        var response = await client.GetAsync($"/api/winners?hackathonId={hackathon.HackathonId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<WinnersResponse>();
        Assert.NotNull(payload);
        Assert.NotNull(payload!.Tracks);
        Assert.Single(payload.Tracks!);
        Assert.Equal("track-1", payload.Tracks![0].TrackId);
        Assert.Single(payload.Tracks![0].Winners!);
        Assert.Equal("idea-winner", payload.Tracks![0].Winners![0].Id);
        Assert.Single(payload.SpecialRecognition!);
        Assert.Equal("idea-special", payload.SpecialRecognition![0].Id);
    }

    [Fact]
    public async Task MarkWinner_ThenWinnersEndpointShowsIdea()
    {
        await using var factory = new TestApiFactory();
        var admin = factory.SeedUser("admin-w1@example.com", UserRole.Admin);
        var participant = factory.SeedUser("participant-w1@example.com", UserRole.Participant);
        var hackathon = factory.SeedHackathon("h-w2", [new Track { Id = "track-2", Name = "Growth" }]);
        var idea = factory.SeedIdea(hackathon.HackathonId, "idea-to-mark", IdeaStatus.Submitted, trackId: "track-2");

        var adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(adminClient, admin.Id, admin.Email, "Admin");

        var markResponse = await adminClient.PostAsJsonAsync("/api/admin/judging/mark-winner", new
        {
            IdeaId = idea.Id,
            HackathonId = hackathon.HackathonId,
            AwardIds = new[] { "award-1" }
        });
        Assert.Equal(HttpStatusCode.OK, markResponse.StatusCode);

        var participantClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(participantClient, participant.Id, participant.Email, "Participant");

        var winnersResponse = await participantClient.GetAsync($"/api/winners?hackathonId={hackathon.HackathonId}");
        Assert.Equal(HttpStatusCode.OK, winnersResponse.StatusCode);
        var payload = await winnersResponse.Content.ReadFromJsonAsync<WinnersResponse>();
        Assert.NotNull(payload);
        var winnerIds = payload!.Tracks!.SelectMany(track => track.Winners ?? []).Select(winner => winner.Id).ToList();
        Assert.Contains("idea-to-mark", winnerIds);
    }

    [Fact]
    public async Task Analytics_IsForbidden_ForParticipant_AndVisible_ForAdmin()
    {
        await using var factory = new TestApiFactory();
        var admin = factory.SeedUser("admin-a1@example.com", UserRole.Admin, UserRole.Judge);
        var participant = factory.SeedUser("participant-a1@example.com", UserRole.Participant);
        var judge = factory.SeedUser("judge-a1@example.com", UserRole.Judge);

        var hackathon = factory.SeedHackathon("h-a1", [new Track { Id = "track-a", Name = "Platform" }]);
        factory.SeedTeam(hackathon.HackathonId, "team-1", 3);
        factory.SeedTeam(hackathon.HackathonId, "team-2", 1);

        factory.SeedIdea(hackathon.HackathonId, "idea-1", IdeaStatus.Draft, trackId: "track-a");
        factory.SeedIdea(hackathon.HackathonId, "idea-2", IdeaStatus.Submitted, trackId: "track-a");
        factory.SeedIdea(hackathon.HackathonId, "idea-3", IdeaStatus.Winner, trackId: null);

        var participantClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(participantClient, participant.Id, participant.Email, "Participant");
        var forbiddenResponse = await participantClient.GetAsync($"/api/analytics?hackathonId={hackathon.HackathonId}");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);

        var adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(adminClient, admin.Id, admin.Email, "Admin,Judge");
        var okResponse = await adminClient.GetAsync($"/api/analytics?hackathonId={hackathon.HackathonId}");

        Assert.Equal(HttpStatusCode.OK, okResponse.StatusCode);
        var payload = await okResponse.Content.ReadFromJsonAsync<AnalyticsResponse>();
        Assert.NotNull(payload);
        Assert.Equal(3, payload!.TotalIdeas);
        Assert.Equal(2, payload.TotalTeams);
        Assert.True(payload.TotalParticipants >= 1);
        Assert.True(payload.TotalJudges >= 2);
        Assert.Equal(1, payload.IdeasByStatus!["draft"]);
        Assert.Equal(1, payload.IdeasByStatus!["submitted"]);
        Assert.Equal(1, payload.IdeasByStatus!["winner"]);
        Assert.Equal(2, payload.TeamSizes!.Count);
    }

    private static void AddAuthHeaders(HttpClient client, string userId, string email, string rolesCsv)
    {
        client.DefaultRequestHeaders.Remove(TestAuthHandler.UserIdHeader);
        client.DefaultRequestHeaders.Remove(TestAuthHandler.EmailHeader);
        client.DefaultRequestHeaders.Remove(TestAuthHandler.RolesHeader);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, email);
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, rolesCsv);
    }

    private sealed class TestApiFactory : WebApplicationFactory<OTG.Api.Program>
    {
        private readonly InMemoryUserRepository userRepository = new();
        private readonly InMemoryIdeaRepository ideaRepository = new();
        private readonly InMemoryHackathonRepository hackathonRepository = new();
        private readonly InMemoryTeamRepository teamRepository = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IUserRepository>();
                services.RemoveAll<IIdeaRepository>();
                services.RemoveAll<IHackathonRepository>();
                services.RemoveAll<ITeamRepository>();

                services
                    .AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

                services.AddSingleton<IUserRepository>(userRepository);
                services.AddSingleton<IIdeaRepository>(ideaRepository);
                services.AddSingleton<IHackathonRepository>(hackathonRepository);
                services.AddSingleton<ITeamRepository>(teamRepository);
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

            userRepository.UpsertAsync(user).GetAwaiter().GetResult();
            return user;
        }

        public Hackathon SeedHackathon(string hackathonId, IReadOnlyList<Track>? tracks = null)
        {
            var hackathon = new Hackathon
            {
                Id = hackathonId,
                HackathonId = hackathonId,
                Name = $"Hackathon-{hackathonId}",
                Tracks = tracks?.ToList() ?? []
            };

            hackathonRepository.UpsertAsync(hackathon).GetAwaiter().GetResult();
            return hackathon;
        }

        public Idea SeedIdea(string hackathonId, string id, IdeaStatus status, string? trackId)
        {
            var idea = new Idea
            {
                Id = id,
                HackathonId = hackathonId,
                AuthorId = Guid.NewGuid().ToString("N"),
                Title = id,
                Description = "desc",
                Status = status,
                TrackId = trackId,
                AwardIds = status == IdeaStatus.Winner ? ["award-default"] : [],
                TermsAccepted = true
            };

            ideaRepository.UpsertAsync(idea).GetAwaiter().GetResult();
            return idea;
        }

        public Team SeedTeam(string hackathonId, string id, int memberCount)
        {
            var members = Enumerable.Range(0, memberCount)
                .Select(_ => new TeamMember { UserId = Guid.NewGuid().ToString("N") })
                .ToList();

            var team = new Team
            {
                Id = id,
                HackathonId = hackathonId,
                Name = id,
                Description = "team",
                LeaderId = members.First().UserId,
                Members = members,
                MaxSize = 8
            };

            teamRepository.UpsertAsync(team).GetAwaiter().GetResult();
            return team;
        }
    }

    private sealed class WinnersResponse
    {
        public string? HackathonId { get; set; }
        public List<TrackWinners>? Tracks { get; set; }
        public List<WinnerItem>? SpecialRecognition { get; set; }
    }

    private sealed class TrackWinners
    {
        public string? TrackId { get; set; }
        public string? TrackName { get; set; }
        public List<WinnerItem>? Winners { get; set; }
    }

    private sealed class WinnerItem
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
    }

    private sealed class AnalyticsResponse
    {
        public string? HackathonId { get; set; }
        public int TotalIdeas { get; set; }
        public int TotalTeams { get; set; }
        public int TotalParticipants { get; set; }
        public int TotalJudges { get; set; }
        public Dictionary<string, int>? IdeasByStatus { get; set; }
        public List<TeamSizeItem>? TeamSizes { get; set; }
    }

    private sealed class TeamSizeItem
    {
        public string? TeamId { get; set; }
        public string? TeamName { get; set; }
        public int MemberCount { get; set; }
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
                EmailConfirmed = user.EmailConfirmed,
                PasswordHash = user.PasswordHash,
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

    private sealed class InMemoryHackathonRepository : IHackathonRepository
    {
        private readonly Dictionary<string, Hackathon> byId = [];

        public Task<Hackathon?> GetByIdAsync(string hackathonId, CancellationToken cancellationToken = default)
            => Task.FromResult(byId.TryGetValue(hackathonId, out var hackathon) ? Clone(hackathon) : null);

        public Task UpsertAsync(Hackathon hackathon, CancellationToken cancellationToken = default)
        {
            byId[hackathon.HackathonId] = Clone(hackathon);
            return Task.CompletedTask;
        }

        private static Hackathon Clone(Hackathon hackathon)
        {
            return new Hackathon
            {
                Id = hackathon.Id,
                HackathonId = hackathon.HackathonId,
                Name = hackathon.Name,
                Tracks = hackathon.Tracks
                    .Select(track => new Track
                    {
                        Id = track.Id,
                        Name = track.Name,
                        Tagline = track.Tagline,
                        Description = track.Description,
                        Icon = track.Icon,
                        BackgroundImageUrl = track.BackgroundImageUrl,
                        SortOrder = track.SortOrder
                    })
                    .ToList(),
                Awards = hackathon.Awards.ToList(),
                Faq = hackathon.Faq.ToList(),
                JudgingCriteria = hackathon.JudgingCriteria.ToList(),
                Milestones = hackathon.Milestones.ToList(),
                CreatedAtUtc = hackathon.CreatedAtUtc,
                UpdatedAtUtc = hackathon.UpdatedAtUtc
            };
        }
    }

    private sealed class InMemoryTeamRepository : ITeamRepository
    {
        private readonly Dictionary<(string HackathonId, string TeamId), Team> byId = [];

        public Task<Team?> GetByIdAsync(string teamId, string hackathonId, CancellationToken cancellationToken = default)
            => Task.FromResult(byId.TryGetValue((hackathonId, teamId), out var team) ? Clone(team) : null);

        public Task<IReadOnlyList<Team>> SearchAsync(string hackathonId, string? query, CancellationToken cancellationToken = default)
        {
            IEnumerable<Team> teams = byId.Values.Where(team => string.Equals(team.HackathonId, hackathonId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(query))
            {
                teams = teams.Where(team => team.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || (team.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            return Task.FromResult<IReadOnlyList<Team>>(teams.Select(Clone).ToList());
        }

        public Task UpsertAsync(Team team, CancellationToken cancellationToken = default)
        {
            byId[(team.HackathonId, team.Id)] = Clone(team);
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
