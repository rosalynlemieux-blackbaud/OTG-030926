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

namespace OTG.IntegrationTests;

public sealed class PeopleIntegrationTests
{
    [Fact]
    public async Task SearchPeople_ReturnsMatchedProfiles_WithTopSkills()
    {
        await using var factory = new TestApiFactory();
        var viewer = factory.SeedUser("viewer@example.com", "View User", "Engineering", ["c#", "cosmosdb", "azure"], "participant");
        factory.SeedUser("alice@example.com", "Alice Cooper", "Product", ["ui", "ux", "a11y", "research"], "participant");
        factory.SeedUser("bob@example.com", "Bob Stone", "Engineering", ["dotnet"], "judge");

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, viewer.Id, viewer.Email, "Participant");

        var response = await client.GetAsync("/api/people?query=alice");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<List<PersonResult>>();
        Assert.NotNull(payload);
        Assert.Single(payload!);
        Assert.Equal("alice@example.com", payload[0].Email);
        Assert.Equal(3, payload[0].TopSkills.Count);
    }

    [Fact]
    public async Task SearchPeople_RequiresAuthentication()
    {
        await using var factory = new TestApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/people");

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

    private sealed class PersonResult
    {
        public string? Email { get; set; }
        public List<string> TopSkills { get; set; } = [];
    }

    private sealed class TestApiFactory : WebApplicationFactory<OTG.Api.Program>
    {
        private readonly InMemoryUserRepository userRepository = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IUserRepository>();

                services
                    .AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

                services.AddSingleton<IUserRepository>(userRepository);
            });
        }

        public User SeedUser(string email, string name, string department, IReadOnlyList<string> skills, string role)
        {
            var id = Guid.NewGuid().ToString("N");
            var user = new User
            {
                Id = id,
                Email = email,
                EmailConfirmed = true,
                Roles = [Enum.Parse<UserRole>(role, true)],
                Profile = new Profile
                {
                    Id = Guid.NewGuid().ToString("N"),
                    UserId = id,
                    Email = email,
                    Name = name,
                    Department = department,
                    Skills = skills.ToList(),
                    Banned = false
                }
            };

            userRepository.UpsertAsync(user).GetAwaiter().GetResult();
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
                        Skills = user.Profile.Skills.ToList(),
                        Banned = user.Profile.Banned
                    }
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
