using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OTG.Api.Services;
using OTG.Application.Abstractions.Repositories;
using OTG.Domain.Identity;

namespace OTG.IntegrationTests;

public sealed class AuthOAuthIntegrationTests
{

    [Fact]
    public async Task BlackbaudCallback_Succeeds_AndRedirects_WithCookie()
    {
        await using var factory = new TestApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var startResponse = await client.GetAsync("/api/auth/blackbaud/start?origin=http://localhost:4200");
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        var payload = await startResponse.Content.ReadFromJsonAsync<StartResponse>();
        Assert.NotNull(payload);

        factory.OAuthService.ShouldThrowForExchange = false;
        factory.OAuthService.UserData = new BlackbaudUserData
        {
            Email = "judge@example.com",
            FullName = "Judge Example",
            BlackbaudId = "bb-123",
            RefreshToken = "refresh-1",
            AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30)
        };

        var callback = await client.GetAsync($"/api/auth/blackbaud/callback?code=test-code&state={payload!.State}");

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.NotNull(callback.Headers.Location);
        Assert.StartsWith("http://localhost:4200", callback.Headers.Location!.ToString());
        Assert.Contains("provider=blackbaud", callback.Headers.Location!.ToString());
        Assert.True(callback.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies!, cookie => cookie.Contains("otg_access_token="));

        var user = await factory.UserRepository.GetByEmailAsync("judge@example.com", CancellationToken.None);
        Assert.NotNull(user);
        Assert.NotNull(user!.Profile);
        Assert.True(user.Profile!.BlackbaudLinked);
        Assert.Equal("refresh-1", user.Profile.BlackbaudRefreshToken);
    }

    [Fact]
    public async Task BlackbaudCallback_ReturnsBadGateway_WhenOAuthExchangeFails()
    {
        await using var factory = new TestApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var startResponse = await client.GetAsync("/api/auth/blackbaud/start?origin=http://localhost:4200");
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        var payload = await startResponse.Content.ReadFromJsonAsync<StartResponse>();
        Assert.NotNull(payload);

        factory.OAuthService.ShouldThrowForExchange = true;

        var callback = await client.GetAsync($"/api/auth/blackbaud/callback?code=test-code&state={payload!.State}");

        Assert.Equal(HttpStatusCode.BadGateway, callback.StatusCode);
        var problem = JsonDocument.Parse(await callback.Content.ReadAsStringAsync());
        Assert.Equal("Blackbaud token exchange failed", problem.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task BlackbaudRefresh_Succeeds_AndPersists_RotatedToken()
    {
        await using var factory = new TestApiFactory();
        var user = CreateUser("refresh-success@example.com", linked: true, refreshToken: "old-refresh");
        await factory.UserRepository.UpsertAsync(user, CancellationToken.None);

        factory.OAuthService.ShouldThrowForRefresh = false;
        factory.OAuthService.RefreshResult = new BlackbaudTokenRefreshResult
        {
            AccessToken = "access-new",
            RefreshToken = "refresh-new",
            AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(45)
        };

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, user.Id);
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, user.Email);
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', user.Roles.Select(r => r.ToString())));

        var response = await client.PostAsync("/api/auth/blackbaud/refresh", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await factory.UserRepository.GetByIdAsync(user.Id, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal("refresh-new", updated!.Profile!.BlackbaudRefreshToken);
    }

    [Fact]
    public async Task BlackbaudRefresh_ReturnsBadRequest_WhenUserNotLinked()
    {
        await using var factory = new TestApiFactory();
        var user = CreateUser("refresh-not-linked@example.com", linked: false, refreshToken: null);
        await factory.UserRepository.UpsertAsync(user, CancellationToken.None);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, user.Id);
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, user.Email);
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', user.Roles.Select(r => r.ToString())));

        var response = await client.PostAsync("/api/auth/blackbaud/refresh", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BlackbaudRefresh_ReturnsBadGateway_WhenRefreshFails()
    {
        await using var factory = new TestApiFactory();
        var user = CreateUser("refresh-fail@example.com", linked: true, refreshToken: "old-refresh");
        await factory.UserRepository.UpsertAsync(user, CancellationToken.None);

        factory.OAuthService.ShouldThrowForRefresh = true;

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, user.Id);
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, user.Email);
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', user.Roles.Select(r => r.ToString())));

        var response = await client.PostAsync("/api/auth/blackbaud/refresh", content: null);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task BlackbaudRefresh_ReturnsForbidden_WhenUserIsBanned()
    {
        await using var factory = new TestApiFactory();
        var user = CreateUser("refresh-banned@example.com", linked: true, refreshToken: "old-refresh");
        user.Profile!.Banned = true;
        await factory.UserRepository.UpsertAsync(user, CancellationToken.None);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, user.Id);
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, user.Email);
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', user.Roles.Select(r => r.ToString())));

        var response = await client.PostAsync("/api/auth/blackbaud/refresh", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private sealed class TestApiFactory : WebApplicationFactory<OTG.Api.Program>
    {
        public InMemoryUserRepository UserRepository { get; } = new();
        public FakeBlackbaudOAuthService OAuthService { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Blackbaud:ApplicationId"] = "test-app-id",
                    ["Blackbaud:ApplicationSecret"] = "test-app-secret",
                    ["Blackbaud:RedirectUri"] = "http://localhost:5001/api/auth/blackbaud/callback",
                    ["Blackbaud:SubscriptionKey"] = "test-sub-key",
                    ["Blackbaud:AllowedOrigins:0"] = "http://localhost:4200"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IUserRepository>();
                services.RemoveAll<IBlackbaudOAuthService>();
                services.RemoveAll<ITokenService>();

                services
                    .AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ =>
                    {
                    });

                services.AddScoped<IUserRepository>(_ => UserRepository);
                services.AddSingleton<IBlackbaudOAuthService>(OAuthService);
                services.AddSingleton<ITokenService, TestTokenService>();
            });
        }
    }

    private sealed class StartResponse
    {
        public string? AuthorizationUrl { get; set; }
        public string? State { get; set; }
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
                        AvatarUrl = user.Profile.AvatarUrl,
                        Department = user.Profile.Department,
                        Location = user.Profile.Location,
                        Skills = user.Profile.Skills.ToList(),
                        Interests = user.Profile.Interests.ToList(),
                        Banned = user.Profile.Banned,
                        BannedAtUtc = user.Profile.BannedAtUtc,
                        BlackbaudId = user.Profile.BlackbaudId,
                        BlackbaudLinked = user.Profile.BlackbaudLinked,
                        FirstName = user.Profile.FirstName,
                        LastName = user.Profile.LastName,
                        Title = user.Profile.Title,
                        Phone = user.Profile.Phone,
                        JobTitle = user.Profile.JobTitle,
                        Organization = user.Profile.Organization,
                        Birthdate = user.Profile.Birthdate,
                        EnvironmentId = user.Profile.EnvironmentId,
                        EnvironmentName = user.Profile.EnvironmentName,
                        LegalEntityId = user.Profile.LegalEntityId,
                        LegalEntityName = user.Profile.LegalEntityName,
                        BlackbaudRefreshToken = user.Profile.BlackbaudRefreshToken,
                        BlackbaudRefreshTokenUpdatedAtUtc = user.Profile.BlackbaudRefreshTokenUpdatedAtUtc,
                        BlackbaudAccessTokenExpiresAtUtc = user.Profile.BlackbaudAccessTokenExpiresAtUtc,
                        MerchantAccounts = user.Profile.MerchantAccounts
                            .Select(account => new MerchantAccount
                            {
                                Name = account.Name,
                                MerchantId = account.MerchantId,
                                Currency = account.Currency,
                                ProcessMode = account.ProcessMode,
                                Active = account.Active
                            })
                            .ToList(),
                        CreatedAtUtc = user.Profile.CreatedAtUtc,
                        UpdatedAtUtc = user.Profile.UpdatedAtUtc
                    },
                CreatedAtUtc = user.CreatedAtUtc,
                UpdatedAtUtc = user.UpdatedAtUtc
            };
        }
    }

    private sealed class FakeBlackbaudOAuthService : IBlackbaudOAuthService
    {
        public bool ShouldThrowForExchange { get; set; }
        public bool ShouldThrowForRefresh { get; set; }
        public BlackbaudUserData UserData { get; set; } = new()
        {
            Email = "participant@example.com",
            RefreshToken = "refresh-default"
        };
        public BlackbaudTokenRefreshResult RefreshResult { get; set; } = new()
        {
            AccessToken = "access-token",
            RefreshToken = "refresh-rotated",
            AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30)
        };

        public Task<BlackbaudUserData> ExchangeCodeForUserAsync(string code, CancellationToken cancellationToken)
        {
            if (ShouldThrowForExchange)
            {
                throw new InvalidOperationException("Simulated OAuth failure");
            }

            return Task.FromResult(UserData);
        }

        public Task<BlackbaudTokenRefreshResult> RefreshAccessTokenAsync(string refreshToken, CancellationToken cancellationToken)
        {
            if (ShouldThrowForRefresh)
            {
                throw new InvalidOperationException("Simulated OAuth refresh failure");
            }

            return Task.FromResult(RefreshResult);
        }
    }

    private sealed class TestTokenService : ITokenService
    {
        public string CreateAccessToken(User user) => $"test-token-{user.Id}";
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

    private static User CreateUser(string email, bool linked, string? refreshToken)
    {
        var userId = Guid.NewGuid().ToString("N");
        return new User
        {
            Id = userId,
            Email = email,
            EmailConfirmed = true,
            Roles = [UserRole.Participant],
            Profile = new Profile
            {
                Id = Guid.NewGuid().ToString("N"),
                UserId = userId,
                Email = email,
                BlackbaudLinked = linked,
                BlackbaudRefreshToken = refreshToken
            }
        };
    }
}
