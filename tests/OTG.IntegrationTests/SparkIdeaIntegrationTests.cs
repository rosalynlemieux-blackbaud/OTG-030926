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

public sealed class SparkIdeaIntegrationTests
{
    [Fact]
    public async Task SparkIdea_CreatesDraftAfterThirdExchange()
    {
        await using var factory = new TestApiFactory();
        var user = factory.SeedUser("spark-user@example.com", UserRole.Participant);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, user.Id, user.Email, "Participant");

        var first = await client.PostAsJsonAsync("/api/ideas/spark", new { HackathonId = "hack-spark", Message = "I want to improve onboarding" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstPayload = await first.Content.ReadFromJsonAsync<SparkResponse>();
        Assert.NotNull(firstPayload);
        Assert.False(firstPayload!.ReadyToSubmit);
        Assert.False(string.IsNullOrWhiteSpace(firstPayload.ConversationId));

        var second = await client.PostAsJsonAsync("/api/ideas/spark", new { ConversationId = firstPayload.ConversationId, HackathonId = "hack-spark", Message = "Targeting new employees" });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondPayload = await second.Content.ReadFromJsonAsync<SparkResponse>();
        Assert.NotNull(secondPayload);
        Assert.False(secondPayload!.ReadyToSubmit);

        var third = await client.PostAsJsonAsync("/api/ideas/spark", new { ConversationId = firstPayload.ConversationId, HackathonId = "hack-spark", Message = "Measure completion and confidence" });
        Assert.Equal(HttpStatusCode.OK, third.StatusCode);
        var thirdPayload = await third.Content.ReadFromJsonAsync<SparkResponse>();
        Assert.NotNull(thirdPayload);
        Assert.True(thirdPayload!.ReadyToSubmit);
        Assert.False(string.IsNullOrWhiteSpace(thirdPayload.IdeaId));

        var savedIdea = await factory.IdeaRepository.GetByIdAsync(thirdPayload.IdeaId!, "hack-spark", CancellationToken.None);
        Assert.NotNull(savedIdea);
        Assert.Equal(user.Id, savedIdea!.AuthorId);
        Assert.Equal(IdeaStatus.Draft, savedIdea.Status);
    }

    [Fact]
    public async Task SparkIdea_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        await using var factory = new TestApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsJsonAsync("/api/ideas/spark", new { HackathonId = "hack", Message = "hello" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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

    private sealed class SparkResponse
    {
        public string? ConversationId { get; set; }
        public bool ReadyToSubmit { get; set; }
        public string? IdeaId { get; set; }
    }

    private sealed class TestApiFactory : WebApplicationFactory<OTG.Api.Program>
    {
        public InMemoryUserRepository UserRepository { get; } = new();
        public InMemoryIdeaRepository IdeaRepository { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IUserRepository>();
                services.RemoveAll<IIdeaRepository>();

                services
                    .AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

                services.AddSingleton<IUserRepository>(UserRepository);
                services.AddSingleton<IIdeaRepository>(IdeaRepository);
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
    }

    private sealed class InMemoryUserRepository : IUserRepository
    {
        private readonly Dictionary<string, User> byId = [];

        public Task<User?> GetByIdAsync(string userId, CancellationToken cancellationToken = default)
            => Task.FromResult(byId.TryGetValue(userId, out var user) ? Clone(user) : null);

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
            => Task.FromResult(byId.Values.FirstOrDefault(user => string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)) is { } user ? Clone(user) : null);

        public Task<IReadOnlyList<User>> SearchAsync(string? query, int limit = 50, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<User>>(byId.Values.Take(Math.Clamp(limit, 1, 100)).Select(Clone).ToList());

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
                        Banned = user.Profile.Banned
                    }
            };
        }
    }

    public sealed class InMemoryIdeaRepository : IIdeaRepository
    {
        private readonly Dictionary<(string HackathonId, string IdeaId), Idea> byId = [];

        public Task<Idea?> GetByIdAsync(string id, string hackathonId, CancellationToken cancellationToken = default)
            => Task.FromResult(byId.TryGetValue((hackathonId, id), out var idea) ? Clone(idea) : null);

        public Task<IReadOnlyList<Idea>> SearchAsync(string hackathonId, IdeaStatus? status, string? trackId, string? searchText, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Idea>>(byId.Values.Where(i => i.HackathonId == hackathonId).Select(Clone).ToList());

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
                TermsAccepted = idea.TermsAccepted,
                UpdatedAtUtc = idea.UpdatedAtUtc
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
