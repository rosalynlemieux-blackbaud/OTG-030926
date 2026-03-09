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

public sealed class AdminUsersIntegrationTests
{
    [Fact]
    public async Task SearchUsers_ReturnsForbidden_ForNonAdmin()
    {
        await using var factory = new TestApiFactory();
        var participant = factory.SeedUser("participant@example.com", UserRole.Participant);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, participant.Id, participant.Email, "Participant");

        var response = await client.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRoles_UpdatesUserRoles()
    {
        await using var factory = new TestApiFactory();
        var admin = factory.SeedUser("admin2@example.com", UserRole.Admin);
        var target = factory.SeedUser("target@example.com", UserRole.Participant);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, admin.Id, admin.Email, "Admin");

        var response = await client.PutAsJsonAsync($"/api/admin/users/{target.Id}/roles", new
        {
            Roles = new[] { "Judge", "Participant" }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await factory.UserRepository.GetByIdAsync(target.Id, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Contains(UserRole.Judge, updated!.Roles);
    }

    [Fact]
    public async Task BanToggle_UpdatesProfileBanStatus()
    {
        await using var factory = new TestApiFactory();
        var admin = factory.SeedUser("admin3@example.com", UserRole.Admin);
        var target = factory.SeedUser("target2@example.com", UserRole.Participant);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, admin.Id, admin.Email, "Admin");

        var response = await client.PutAsJsonAsync($"/api/admin/users/{target.Id}/ban", new
        {
            Banned = true
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await factory.UserRepository.GetByIdAsync(target.Id, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.True(updated!.Profile!.Banned);
        Assert.NotNull(updated.Profile.BannedAtUtc);
    }

    [Fact]
    public async Task UpdateRoles_ReturnsBadRequest_ForInvalidRole()
    {
        await using var factory = new TestApiFactory();
        var admin = factory.SeedUser("admin4@example.com", UserRole.Admin);
        var target = factory.SeedUser("target3@example.com", UserRole.Participant);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, admin.Id, admin.Email, "Admin");

        var response = await client.PutAsJsonAsync($"/api/admin/users/{target.Id}/roles", new
        {
            Roles = new[] { "Nope" }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRoles_ReturnsBadRequest_WhenSelfDemoting()
    {
        await using var factory = new TestApiFactory();
        var admin = factory.SeedUser("admin5@example.com", UserRole.Admin);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, admin.Id, admin.Email, "Admin");

        var response = await client.PutAsJsonAsync($"/api/admin/users/{admin.Id}/roles", new
        {
            Roles = new[] { "Participant" }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BanToggle_ReturnsBadRequest_WhenSelfBanning()
    {
        await using var factory = new TestApiFactory();
        var admin = factory.SeedUser("admin6@example.com", UserRole.Admin);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, admin.Id, admin.Email, "Admin");

        var response = await client.PutAsJsonAsync($"/api/admin/users/{admin.Id}/ban", new
        {
            Banned = true
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRoles_ReturnsConflict_WhenRemovingLastAdmin()
    {
        await using var factory = new TestApiFactory();
        var admin = factory.SeedUser("admin7@example.com", UserRole.Admin);
        var otherAdmin = factory.SeedUser("admin8@example.com", UserRole.Admin);
        otherAdmin.Profile!.Banned = true;
        await factory.UserRepository.UpsertAsync(otherAdmin, CancellationToken.None);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, admin.Id, admin.Email, "Admin");

        var response = await client.PutAsJsonAsync($"/api/admin/users/{otherAdmin.Id}/roles", new
        {
            Roles = new[] { "Participant" }
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
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
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ =>
                    {
                    });

                services.AddSingleton<IUserRepository>(UserRepository);
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
