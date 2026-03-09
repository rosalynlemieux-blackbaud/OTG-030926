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

namespace OTG.IntegrationTests;

public sealed class JudgingIntegrationTests
{
    [Fact]
    public async Task AssignedIdeas_ReturnsOnlyIdeasAssignedToJudge()
    {
        await using var factory = new TestApiFactory();

        var judge = factory.SeedUser("judge-1@example.com", UserRole.Judge);
        var otherJudge = factory.SeedUser("judge-2@example.com", UserRole.Judge);
        var hackathon = factory.SeedHackathon("h-1");
        factory.SeedIdea(hackathon.HackathonId, "idea-a", assignedJudgeIds: [judge.Id]);
        factory.SeedIdea(hackathon.HackathonId, "idea-b", assignedJudgeIds: [otherJudge.Id]);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, judge.Id, judge.Email, "Judge");

        var response = await client.GetAsync($"/api/judging/assigned?hackathonId={hackathon.HackathonId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ideas = await response.Content.ReadFromJsonAsync<List<Idea>>();
        Assert.NotNull(ideas);
        Assert.Single(ideas!);
        Assert.Equal("idea-a", ideas[0].Id);
    }

    [Fact]
    public async Task AssignedIdeas_ReturnsForbidden_ForParticipant()
    {
        await using var factory = new TestApiFactory();

        var participant = factory.SeedUser("participant@example.com", UserRole.Participant);
        var hackathon = factory.SeedHackathon("h-2");

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, participant.Id, participant.Email, "Participant");

        var response = await client.GetAsync($"/api/judging/assigned?hackathonId={hackathon.HackathonId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SubmitRating_ReturnsForbidden_WhenJudgeNotAssigned()
    {
        await using var factory = new TestApiFactory();

        var judge = factory.SeedUser("judge-3@example.com", UserRole.Judge);
        var hackathon = factory.SeedHackathon("h-3", [new JudgingCriterion
        {
            Id = "impact",
            Name = "Impact",
            MaxScore = 5,
            Weight = 1
        }]);
        var idea = factory.SeedIdea(hackathon.HackathonId, "idea-unassigned", assignedJudgeIds: []);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, judge.Id, judge.Email, "Judge");

        var response = await client.PostAsJsonAsync("/api/judging/ratings", new
        {
            IdeaId = idea.Id,
            HackathonId = hackathon.HackathonId,
            Scores = new[]
            {
                new { CriterionId = "impact", Score = 4, Feedback = "good" }
            },
            OverallFeedback = "Looks solid"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminAssignJudge_ThenJudgeCanSubmitRating()
    {
        await using var factory = new TestApiFactory();

        var admin = factory.SeedUser("admin@example.com", UserRole.Admin);
        var judge = factory.SeedUser("judge-4@example.com", UserRole.Judge);
        var hackathon = factory.SeedHackathon("h-4", [new JudgingCriterion
        {
            Id = "innovation",
            Name = "Innovation",
            MaxScore = 10,
            Weight = 2
        }]);
        var idea = factory.SeedIdea(hackathon.HackathonId, "idea-assign", assignedJudgeIds: []);

        var adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(adminClient, admin.Id, admin.Email, "Admin");

        var assignResponse = await adminClient.PostAsJsonAsync("/api/admin/judging/assign-judge", new
        {
            IdeaId = idea.Id,
            HackathonId = hackathon.HackathonId,
            JudgeUserId = judge.Id
        });

        Assert.Equal(HttpStatusCode.OK, assignResponse.StatusCode);

        var judgeClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(judgeClient, judge.Id, judge.Email, "Judge");

        var ratingResponse = await judgeClient.PostAsJsonAsync("/api/judging/ratings", new
        {
            IdeaId = idea.Id,
            HackathonId = hackathon.HackathonId,
            Scores = new[]
            {
                new { CriterionId = "innovation", Score = 8, Feedback = "Strong originality" }
            },
            OverallFeedback = "Great potential"
        });

        Assert.Equal(HttpStatusCode.OK, ratingResponse.StatusCode);
        var rating = await ratingResponse.Content.ReadFromJsonAsync<IdeaRating>();
        Assert.NotNull(rating);
        Assert.Equal(judge.Id, rating!.JudgeId);
        Assert.Equal(80m, rating.WeightedScore);
    }

    [Fact]
    public async Task SubmitRating_ReturnsBadRequest_WhenCriterionIdUnknownForHackathon()
    {
        await using var factory = new TestApiFactory();

        var judge = factory.SeedUser("judge-unknown-criterion@example.com", UserRole.Judge);
        var hackathon = factory.SeedHackathon("h-unknown-criterion", [new JudgingCriterion
        {
            Id = "impact",
            Name = "Impact",
            MaxScore = 5,
            Weight = 1
        }]);
        var idea = factory.SeedIdea(hackathon.HackathonId, "idea-unknown-criterion", assignedJudgeIds: [judge.Id]);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, judge.Id, judge.Email, "Judge");

        var response = await client.PostAsJsonAsync("/api/judging/ratings", new
        {
            IdeaId = idea.Id,
            HackathonId = hackathon.HackathonId,
            Scores = new[]
            {
                new { CriterionId = "unknown", Score = 4, Feedback = "bad criterion id" }
            },
            OverallFeedback = "test"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SubmitRating_ReturnsBadRequest_WhenCriterionScoreOutOfBounds()
    {
        await using var factory = new TestApiFactory();

        var judge = factory.SeedUser("judge-oob-score@example.com", UserRole.Judge);
        var hackathon = factory.SeedHackathon("h-oob-score", [new JudgingCriterion
        {
            Id = "impact",
            Name = "Impact",
            MaxScore = 5,
            Weight = 1
        }]);
        var idea = factory.SeedIdea(hackathon.HackathonId, "idea-oob-score", assignedJudgeIds: [judge.Id]);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, judge.Id, judge.Email, "Judge");

        var response = await client.PostAsJsonAsync("/api/judging/ratings", new
        {
            IdeaId = idea.Id,
            HackathonId = hackathon.HackathonId,
            Scores = new[]
            {
                new { CriterionId = "impact", Score = 8, Feedback = "over max" }
            },
            OverallFeedback = "test"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
        private readonly InMemoryUserRepository userRepository = new();
        private readonly InMemoryIdeaRepository ideaRepository = new();
        private readonly InMemoryRatingRepository ratingRepository = new();
        private readonly InMemoryHackathonRepository hackathonRepository = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IUserRepository>();
                services.RemoveAll<IIdeaRepository>();
                services.RemoveAll<IRatingRepository>();
                services.RemoveAll<IHackathonRepository>();

                services
                    .AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ =>
                    {
                    });

                services.AddSingleton<IUserRepository>(userRepository);
                services.AddSingleton<IIdeaRepository>(ideaRepository);
                services.AddSingleton<IRatingRepository>(ratingRepository);
                services.AddSingleton<IHackathonRepository>(hackathonRepository);
            });
        }

        public User SeedUser(string email, params UserRole[] roles)
        {
            var userId = Guid.NewGuid().ToString("N");
            var user = new User
            {
                Id = userId,
                Email = email,
                EmailConfirmed = true,
                Roles = roles.Any() ? roles.ToList() : [UserRole.Participant],
                Profile = new Profile
                {
                    Id = Guid.NewGuid().ToString("N"),
                    UserId = userId,
                    Email = email,
                    Banned = false
                }
            };

            userRepository.UpsertAsync(user).GetAwaiter().GetResult();
            return user;
        }

        public Hackathon SeedHackathon(string hackathonId, IReadOnlyList<JudgingCriterion>? criteria = null)
        {
            var hackathon = new Hackathon
            {
                Id = hackathonId,
                HackathonId = hackathonId,
                Name = $"Hackathon-{hackathonId}",
                JudgingCriteria = criteria?.ToList() ?? []
            };

            hackathonRepository.UpsertAsync(hackathon).GetAwaiter().GetResult();
            return hackathon;
        }

        public Idea SeedIdea(string hackathonId, string ideaId, IReadOnlyList<string> assignedJudgeIds)
        {
            var idea = new Idea
            {
                Id = ideaId,
                HackathonId = hackathonId,
                AuthorId = Guid.NewGuid().ToString("N"),
                Title = ideaId,
                Description = "test",
                Status = IdeaStatus.Submitted,
                AssignedJudgeIds = assignedJudgeIds.ToList()
            };

            ideaRepository.UpsertAsync(idea).GetAwaiter().GetResult();
            return idea;
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
                        Banned = user.Profile.Banned,
                        BannedAtUtc = user.Profile.BannedAtUtc
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
            var ideas = byId.Values.Where(idea => string.Equals(idea.HackathonId, hackathonId, StringComparison.Ordinal));
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
                ideas = ideas.Where(idea => (idea.Title?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (idea.Description?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false));
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
                Title = idea.Title,
                Description = idea.Description,
                Status = idea.Status,
                AuthorId = idea.AuthorId,
                TeamId = idea.TeamId,
                TrackId = idea.TrackId,
                Tags = idea.Tags.ToList(),
                Attachments = idea.Attachments.ToList(),
                VideoUrl = idea.VideoUrl,
                RepoUrl = idea.RepoUrl,
                DemoUrl = idea.DemoUrl,
                Votes = idea.Votes,
                SubmittedAtUtc = idea.SubmittedAtUtc,
                AssignedJudgeIds = idea.AssignedJudgeIds.ToList(),
                AwardIds = idea.AwardIds.ToList(),
                TermsAccepted = idea.TermsAccepted,
                CreatedAtUtc = idea.CreatedAtUtc,
                UpdatedAtUtc = idea.UpdatedAtUtc
            };
        }
    }

    private sealed class InMemoryRatingRepository : IRatingRepository
    {
        private readonly List<IdeaRating> ratings = [];

        public Task<IReadOnlyList<IdeaRating>> GetByIdeaAsync(string ideaId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<IdeaRating>>(ratings
                .Where(rating => string.Equals(rating.IdeaId, ideaId, StringComparison.Ordinal))
                .Select(Clone)
                .ToList());
        }

        public Task UpsertAsync(IdeaRating rating, CancellationToken cancellationToken = default)
        {
            var index = ratings.FindIndex(existing => string.Equals(existing.Id, rating.Id, StringComparison.Ordinal));
            if (index >= 0)
            {
                ratings[index] = Clone(rating);
            }
            else
            {
                ratings.Add(Clone(rating));
            }

            return Task.CompletedTask;
        }

        private static IdeaRating Clone(IdeaRating rating)
        {
            return new IdeaRating
            {
                Id = rating.Id,
                IdeaId = rating.IdeaId,
                JudgeId = rating.JudgeId,
                Scores = rating.Scores
                    .Select(score => new CriterionScore
                    {
                        CriterionId = score.CriterionId,
                        Score = score.Score,
                        Feedback = score.Feedback
                    })
                    .ToList(),
                OverallFeedback = rating.OverallFeedback,
                WeightedScore = rating.WeightedScore,
                CreatedAtUtc = rating.CreatedAtUtc,
                UpdatedAtUtc = rating.UpdatedAtUtc
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
                Description = hackathon.Description,
                JudgingCriteria = hackathon.JudgingCriteria
                    .Select(criterion => new JudgingCriterion
                    {
                        Id = criterion.Id,
                        Name = criterion.Name,
                        Description = criterion.Description,
                        BulletPoints = criterion.BulletPoints.ToList(),
                        Icon = criterion.Icon,
                        MaxScore = criterion.MaxScore,
                        Weight = criterion.Weight,
                        SortOrder = criterion.SortOrder
                    })
                    .ToList(),
                Tracks = hackathon.Tracks.ToList(),
                Awards = hackathon.Awards.ToList(),
                Milestones = hackathon.Milestones.ToList(),
                Faq = hackathon.Faq.ToList(),
                CreatedAtUtc = hackathon.CreatedAtUtc,
                UpdatedAtUtc = hackathon.UpdatedAtUtc
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
