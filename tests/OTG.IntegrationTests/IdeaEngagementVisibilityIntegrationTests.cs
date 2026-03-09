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

namespace OTG.IntegrationTests;

public sealed class IdeaEngagementVisibilityIntegrationTests
{
    [Fact]
    public async Task CreateComment_CreatesTopLevelComment_ForParticipant()
    {
        await using var factory = new TestApiFactory();
        var participant = factory.SeedUser("participant-create-comment@example.com", UserRole.Participant);
        var idea = factory.SeedIdea("hack-comment-create", "idea-comment-create", participant.Id);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, participant.Id, participant.Email, "Participant");

        var response = await client.PostAsJsonAsync($"/api/ideas/{idea.Id}/comments?hackathonId={idea.HackathonId}", new
        {
            Content = "New top-level comment"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<Comment>();
        Assert.NotNull(created);
        Assert.Equal(participant.Id, created!.AuthorId);
        Assert.Null(created.ParentId);
    }

    [Fact]
    public async Task CreateComment_AllowsReplyToTopLevelComment()
    {
        await using var factory = new TestApiFactory();
        var author = factory.SeedUser("author-reply@example.com", UserRole.Participant);
        var replier = factory.SeedUser("replier@example.com", UserRole.Participant);
        var idea = factory.SeedIdea("hack-comment-reply", "idea-comment-reply", author.Id);
        var parent = factory.SeedComment(idea.Id, author.Id, isModerated: false);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, replier.Id, replier.Email, "Participant");

        var response = await client.PostAsJsonAsync($"/api/ideas/{idea.Id}/comments?hackathonId={idea.HackathonId}", new
        {
            Content = "Reply",
            ParentId = parent.Id
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<Comment>();
        Assert.NotNull(created);
        Assert.Equal(parent.Id, created!.ParentId);
    }

    [Fact]
    public async Task CreateComment_RejectsReplyToReplyDepth()
    {
        await using var factory = new TestApiFactory();
        var author = factory.SeedUser("author-depth@example.com", UserRole.Participant);
        var replier = factory.SeedUser("replier-depth@example.com", UserRole.Participant);
        var idea = factory.SeedIdea("hack-comment-depth", "idea-comment-depth", author.Id);
        var parent = factory.SeedComment(idea.Id, author.Id, isModerated: false);
        var child = factory.SeedComment(idea.Id, replier.Id, isModerated: false, parentId: parent.Id);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, author.Id, author.Email, "Participant");

        var response = await client.PostAsJsonAsync($"/api/ideas/{idea.Id}/comments?hackathonId={idea.HackathonId}", new
        {
            Content = "Too deep",
            ParentId = child.Id
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateComment_Succeeds_ForAuthor()
    {
        await using var factory = new TestApiFactory();
        var author = factory.SeedUser("author-update@example.com", UserRole.Participant);
        var idea = factory.SeedIdea("hack-comment-update", "idea-comment-update", author.Id);
        var comment = factory.SeedComment(idea.Id, author.Id, isModerated: false);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, author.Id, author.Email, "Participant");

        var response = await client.PutAsJsonAsync($"/api/ideas/{idea.Id}/comments/{comment.Id}?hackathonId={idea.HackathonId}", new
        {
            Content = "Edited content"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<Comment>();
        Assert.NotNull(updated);
        Assert.Equal("Edited content", updated!.Content);
    }

    [Fact]
    public async Task UpdateComment_ReturnsForbidden_ForNonAuthorParticipant()
    {
        await using var factory = new TestApiFactory();
        var author = factory.SeedUser("author-update-forbid@example.com", UserRole.Participant);
        var other = factory.SeedUser("other-update-forbid@example.com", UserRole.Participant);
        var idea = factory.SeedIdea("hack-comment-update-forbid", "idea-comment-update-forbid", author.Id);
        var comment = factory.SeedComment(idea.Id, author.Id, isModerated: false);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, other.Id, other.Email, "Participant");

        var response = await client.PutAsJsonAsync($"/api/ideas/{idea.Id}/comments/{comment.Id}?hackathonId={idea.HackathonId}", new
        {
            Content = "Attempted edit"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateComment_Succeeds_ForAdmin()
    {
        await using var factory = new TestApiFactory();
        var author = factory.SeedUser("author-update-admin@example.com", UserRole.Participant);
        var admin = factory.SeedUser("admin-update-comment@example.com", UserRole.Admin);
        var idea = factory.SeedIdea("hack-comment-update-admin", "idea-comment-update-admin", author.Id);
        var comment = factory.SeedComment(idea.Id, author.Id, isModerated: false);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, admin.Id, admin.Email, "Admin");

        var response = await client.PutAsJsonAsync($"/api/ideas/{idea.Id}/comments/{comment.Id}?hackathonId={idea.HackathonId}", new
        {
            Content = "Edited by admin"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<Comment>();
        Assert.NotNull(updated);
        Assert.Equal("Edited by admin", updated!.Content);
    }

    [Fact]
    public async Task GetComments_HidesModeratedComments_ForParticipant()
    {
        await using var factory = new TestApiFactory();
        var participant = factory.SeedUser("participant-view-comments@example.com", UserRole.Participant);
        var author = factory.SeedUser("author-view-comments@example.com", UserRole.Participant);
        var idea = factory.SeedIdea("hack-view-1", "idea-view-1", author.Id);
        factory.SeedComment(idea.Id, author.Id, isModerated: false);
        factory.SeedComment(idea.Id, author.Id, isModerated: true);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, participant.Id, participant.Email, "Participant");

        var response = await client.GetAsync($"/api/ideas/{idea.Id}/comments?hackathonId={idea.HackathonId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var comments = await response.Content.ReadFromJsonAsync<List<Comment>>();
        Assert.NotNull(comments);
        Assert.Single(comments!);
        Assert.All(comments!, item => Assert.False(item.IsModerated));
    }

    [Fact]
    public async Task GetComments_ReturnsAllComments_ForAdmin()
    {
        await using var factory = new TestApiFactory();
        var admin = factory.SeedUser("admin-view-comments@example.com", UserRole.Admin);
        var author = factory.SeedUser("author-view-comments2@example.com", UserRole.Participant);
        var idea = factory.SeedIdea("hack-view-2", "idea-view-2", author.Id);
        factory.SeedComment(idea.Id, author.Id, isModerated: false);
        factory.SeedComment(idea.Id, author.Id, isModerated: true);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, admin.Id, admin.Email, "Admin");

        var response = await client.GetAsync($"/api/ideas/{idea.Id}/comments?hackathonId={idea.HackathonId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var comments = await response.Content.ReadFromJsonAsync<List<Comment>>();
        Assert.NotNull(comments);
        Assert.Equal(2, comments!.Count);
    }

    [Fact]
    public async Task GetRatings_HidesModeratedRatings_ForJudge()
    {
        await using var factory = new TestApiFactory();
        var judge = factory.SeedUser("judge-view-ratings@example.com", UserRole.Judge);
        var author = factory.SeedUser("author-view-ratings@example.com", UserRole.Participant);
        var idea = factory.SeedIdea("hack-view-3", "idea-view-3", author.Id);
        factory.SeedRating(idea.Id, judge.Id, isModerated: false);
        factory.SeedRating(idea.Id, Guid.NewGuid().ToString("N"), isModerated: true);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, judge.Id, judge.Email, "Judge");

        var response = await client.GetAsync($"/api/ideas/{idea.Id}/ratings?hackathonId={idea.HackathonId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ratings = await response.Content.ReadFromJsonAsync<List<IdeaRating>>();
        Assert.NotNull(ratings);
        Assert.Single(ratings!);
        Assert.All(ratings!, item => Assert.False(item.IsModerated));
    }

    [Fact]
    public async Task GetRatings_ReturnsAllRatings_ForAdmin()
    {
        await using var factory = new TestApiFactory();
        var admin = factory.SeedUser("admin-view-ratings@example.com", UserRole.Admin);
        var judge = factory.SeedUser("judge-view-ratings2@example.com", UserRole.Judge);
        var author = factory.SeedUser("author-view-ratings2@example.com", UserRole.Participant);
        var idea = factory.SeedIdea("hack-view-4", "idea-view-4", author.Id);
        factory.SeedRating(idea.Id, judge.Id, isModerated: false);
        factory.SeedRating(idea.Id, Guid.NewGuid().ToString("N"), isModerated: true);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, admin.Id, admin.Email, "Admin");

        var response = await client.GetAsync($"/api/ideas/{idea.Id}/ratings?hackathonId={idea.HackathonId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ratings = await response.Content.ReadFromJsonAsync<List<IdeaRating>>();
        Assert.NotNull(ratings);
        Assert.Equal(2, ratings!.Count);
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
        public InMemoryCommentRepository CommentRepository { get; } = new();
        public InMemoryRatingRepository RatingRepository { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IUserRepository>();
                services.RemoveAll<IIdeaRepository>();
                services.RemoveAll<ICommentRepository>();
                services.RemoveAll<IRatingRepository>();

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
                services.AddSingleton<ICommentRepository>(CommentRepository);
                services.AddSingleton<IRatingRepository>(RatingRepository);
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
                Title = ideaId,
                Description = "test",
                Status = IdeaStatus.Submitted,
                AuthorId = authorId
            };

            IdeaRepository.UpsertAsync(idea).GetAwaiter().GetResult();
            return idea;
        }

        public Comment SeedComment(string ideaId, string authorId, bool isModerated, string? parentId = null)
        {
            var comment = new Comment
            {
                Id = Guid.NewGuid().ToString("N"),
                IdeaId = ideaId,
                AuthorId = authorId,
                Content = "Comment",
            ParentId = parentId,
                IsModerated = isModerated,
                ModerationReason = isModerated ? "Reason" : null,
                ModeratedBy = isModerated ? Guid.NewGuid().ToString("N") : null,
                ModeratedAtUtc = isModerated ? DateTimeOffset.UtcNow : null
            };

            CommentRepository.UpsertAsync(comment).GetAwaiter().GetResult();
            return comment;
        }

        public IdeaRating SeedRating(string ideaId, string judgeId, bool isModerated)
        {
            var rating = new IdeaRating
            {
                Id = Guid.NewGuid().ToString("N"),
                IdeaId = ideaId,
                JudgeId = judgeId,
                Scores = [new CriterionScore { CriterionId = "impact", Score = 5 }],
                WeightedScore = 50,
                IsModerated = isModerated,
                ModerationReason = isModerated ? "Reason" : null,
                ModeratedBy = isModerated ? Guid.NewGuid().ToString("N") : null,
                ModeratedAtUtc = isModerated ? DateTimeOffset.UtcNow : null
            };

            RatingRepository.UpsertAsync(rating).GetAwaiter().GetResult();
            return rating;
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

    private sealed class InMemoryCommentRepository : ICommentRepository
    {
        private readonly Dictionary<string, Comment> byId = [];

        public Task<IReadOnlyList<Comment>> GetByIdeaAsync(string ideaId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Comment>>(byId.Values
                .Where(item => string.Equals(item.IdeaId, ideaId, StringComparison.Ordinal))
                .Select(Clone)
                .ToList());
        }

        public Task UpsertAsync(Comment comment, CancellationToken cancellationToken = default)
        {
            byId[comment.Id] = Clone(comment);
            return Task.CompletedTask;
        }

        private static Comment Clone(Comment comment)
        {
            return new Comment
            {
                Id = comment.Id,
                IdeaId = comment.IdeaId,
                AuthorId = comment.AuthorId,
                Content = comment.Content,
                ParentId = comment.ParentId,
                IsModerated = comment.IsModerated,
                ModerationReason = comment.ModerationReason,
                ModeratedBy = comment.ModeratedBy,
                ModeratedAtUtc = comment.ModeratedAtUtc,
                CreatedAtUtc = comment.CreatedAtUtc,
                UpdatedAtUtc = comment.UpdatedAtUtc
            };
        }
    }

    private sealed class InMemoryRatingRepository : IRatingRepository
    {
        private readonly Dictionary<string, IdeaRating> byId = [];

        public Task<IReadOnlyList<IdeaRating>> GetByIdeaAsync(string ideaId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<IdeaRating>>(byId.Values
                .Where(item => string.Equals(item.IdeaId, ideaId, StringComparison.Ordinal))
                .Select(Clone)
                .ToList());
        }

        public Task UpsertAsync(IdeaRating rating, CancellationToken cancellationToken = default)
        {
            byId[rating.Id] = Clone(rating);
            return Task.CompletedTask;
        }

        private static IdeaRating Clone(IdeaRating rating)
        {
            return new IdeaRating
            {
                Id = rating.Id,
                IdeaId = rating.IdeaId,
                JudgeId = rating.JudgeId,
                Scores = rating.Scores.Select(score => new CriterionScore
                {
                    CriterionId = score.CriterionId,
                    Score = score.Score,
                    Feedback = score.Feedback
                }).ToList(),
                OverallFeedback = rating.OverallFeedback,
                WeightedScore = rating.WeightedScore,
                IsModerated = rating.IsModerated,
                ModerationReason = rating.ModerationReason,
                ModeratedBy = rating.ModeratedBy,
                ModeratedAtUtc = rating.ModeratedAtUtc,
                CreatedAtUtc = rating.CreatedAtUtc,
                UpdatedAtUtc = rating.UpdatedAtUtc
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
