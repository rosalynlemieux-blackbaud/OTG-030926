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
using OTG.Domain.Teams;

namespace OTG.IntegrationTests;

public sealed class TeamWorkflowIntegrationTests
{
    [Fact]
    public async Task CreateJoinRequest_ThenLeaderApproves_AddsMember()
    {
        await using var factory = new TestApiFactory();
        var leader = factory.SeedUser("leader@example.com", UserRole.Participant);
        var candidate = factory.SeedUser("candidate@example.com", UserRole.Participant);
        var team = factory.SeedTeam("hack-join", "team-join", leader.Id, maxSize: 4, members: [leader.Id]);

        var participantClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(participantClient, candidate.Id, candidate.Email, "Participant");

        var createResponse = await participantClient.PostAsJsonAsync($"/api/teams/{team.Id}/join-requests?hackathonId={team.HackathonId}", new
        {
            Message = "I can help with frontend"
        });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<TeamJoinRequest>();
        Assert.NotNull(created);

        var leaderClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(leaderClient, leader.Id, leader.Email, "Participant");

        var approveResponse = await leaderClient.PostAsync($"/api/teams/{team.Id}/join-requests/{created!.Id}/approve?hackathonId={team.HackathonId}", null);

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        var updatedTeam = await factory.TeamRepository.GetByIdAsync(team.Id, team.HackathonId, CancellationToken.None);
        Assert.NotNull(updatedTeam);
        Assert.Contains(updatedTeam!.Members, member => string.Equals(member.UserId, candidate.Id, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ApproveJoinRequest_ReturnsConflict_WhenTeamFull()
    {
        await using var factory = new TestApiFactory();
        var leader = factory.SeedUser("leader-full@example.com", UserRole.Participant);
        var member = factory.SeedUser("member-full@example.com", UserRole.Participant);
        var candidate = factory.SeedUser("candidate-full@example.com", UserRole.Participant);
        var team = factory.SeedTeam("hack-full", "team-full", leader.Id, maxSize: 2, members: [leader.Id, member.Id]);
        var joinRequest = factory.SeedJoinRequest(team.Id, candidate.Id);

        var leaderClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(leaderClient, leader.Id, leader.Email, "Participant");

        var response = await leaderClient.PostAsync($"/api/teams/{team.Id}/join-requests/{joinRequest.Id}/approve?hackathonId={team.HackathonId}", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetJoinRequests_ReturnsForbidden_ForNonLeaderParticipant()
    {
        await using var factory = new TestApiFactory();
        var leader = factory.SeedUser("leader-read@example.com", UserRole.Participant);
        var otherParticipant = factory.SeedUser("other-read@example.com", UserRole.Participant);
        var team = factory.SeedTeam("hack-read", "team-read", leader.Id, maxSize: 3, members: [leader.Id]);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, otherParticipant.Id, otherParticipant.Email, "Participant");

        var response = await client.GetAsync($"/api/teams/{team.Id}/join-requests?hackathonId={team.HackathonId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AcceptInvite_RequiresApproval_WhenInviteNeedsApproval()
    {
        await using var factory = new TestApiFactory();
        var leader = factory.SeedUser("leader-invite@example.com", UserRole.Participant);
        var invitee = factory.SeedUser("invitee-pending@example.com", UserRole.Participant);
        var team = factory.SeedTeam("hack-invite", "team-invite", leader.Id, maxSize: 3, members: [leader.Id]);
        var invite = factory.SeedInvite(team.Id, invitee.Email, leader.Id, needsApproval: true);

        var inviteeClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(inviteeClient, invitee.Id, invitee.Email, "Participant");

        var response = await inviteeClient.PostAsync($"/api/teams/{team.Id}/invites/accept?hackathonId={team.HackathonId}&token={invite.Token}", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ApproveInvite_ThenAcceptInvite_AddsMember()
    {
        await using var factory = new TestApiFactory();
        var leader = factory.SeedUser("leader-approved@example.com", UserRole.Participant);
        var invitee = factory.SeedUser("invitee-approved@example.com", UserRole.Participant);
        var team = factory.SeedTeam("hack-approved", "team-approved", leader.Id, maxSize: 3, members: [leader.Id]);
        var invite = factory.SeedInvite(team.Id, invitee.Email, leader.Id, needsApproval: true);

        var leaderClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(leaderClient, leader.Id, leader.Email, "Participant");

        var approveResponse = await leaderClient.PostAsync($"/api/teams/{team.Id}/invites/{invite.Id}/approve?hackathonId={team.HackathonId}", null);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        var inviteeClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(inviteeClient, invitee.Id, invitee.Email, "Participant");

        var acceptResponse = await inviteeClient.PostAsync($"/api/teams/{team.Id}/invites/accept?hackathonId={team.HackathonId}&token={invite.Token}", null);
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        var updatedTeam = await factory.TeamRepository.GetByIdAsync(team.Id, team.HackathonId, CancellationToken.None);
        Assert.NotNull(updatedTeam);
        Assert.Contains(updatedTeam!.Members, member => string.Equals(member.UserId, invitee.Id, StringComparison.Ordinal));
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
        public InMemoryTeamRepository TeamRepository { get; } = new();
        public InMemoryTeamJoinRequestRepository JoinRequestRepository { get; } = new();
        public InMemoryTeamInviteRepository InviteRepository { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IUserRepository>();
                services.RemoveAll<ITeamRepository>();
                services.RemoveAll<ITeamJoinRequestRepository>();
                services.RemoveAll<ITeamInviteRepository>();

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
                services.AddSingleton<ITeamRepository>(TeamRepository);
                services.AddSingleton<ITeamJoinRequestRepository>(JoinRequestRepository);
                services.AddSingleton<ITeamInviteRepository>(InviteRepository);
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

        public Team SeedTeam(string hackathonId, string teamId, string leaderId, int maxSize, IReadOnlyList<string> members)
        {
            var team = new Team
            {
                Id = teamId,
                HackathonId = hackathonId,
                Name = teamId,
                LeaderId = leaderId,
                MaxSize = maxSize,
                Members = members.Select(memberId => new TeamMember { UserId = memberId }).ToList()
            };

            TeamRepository.UpsertAsync(team).GetAwaiter().GetResult();
            return team;
        }

        public TeamJoinRequest SeedJoinRequest(string teamId, string userId)
        {
            var request = new TeamJoinRequest
            {
                Id = Guid.NewGuid().ToString("N"),
                TeamId = teamId,
                UserId = userId,
                Status = TeamJoinRequestStatus.Pending
            };

            JoinRequestRepository.UpsertAsync(request).GetAwaiter().GetResult();
            return request;
        }

        public TeamInvite SeedInvite(string teamId, string email, string invitedBy, bool needsApproval)
        {
            var invite = new TeamInvite
            {
                Id = Guid.NewGuid().ToString("N"),
                TeamId = teamId,
                Email = email,
                InvitedBy = invitedBy,
                Token = Guid.NewGuid().ToString("N"),
                NeedsApproval = needsApproval,
                Status = TeamInviteStatus.Pending
            };

            InviteRepository.UpsertAsync(invite).GetAwaiter().GetResult();
            return invite;
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

    public sealed class InMemoryTeamRepository : ITeamRepository
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

    public sealed class InMemoryTeamJoinRequestRepository : ITeamJoinRequestRepository
    {
        private readonly Dictionary<string, TeamJoinRequest> byId = [];

        public Task<IReadOnlyList<TeamJoinRequest>> GetByTeamAsync(string teamId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<TeamJoinRequest>>(byId.Values
                .Where(item => string.Equals(item.TeamId, teamId, StringComparison.Ordinal))
                .Select(Clone)
                .ToList());
        }

        public Task UpsertAsync(TeamJoinRequest request, CancellationToken cancellationToken = default)
        {
            byId[request.Id] = Clone(request);
            return Task.CompletedTask;
        }

        private static TeamJoinRequest Clone(TeamJoinRequest request)
        {
            return new TeamJoinRequest
            {
                Id = request.Id,
                TeamId = request.TeamId,
                UserId = request.UserId,
                Message = request.Message,
                Status = request.Status,
                CreatedAtUtc = request.CreatedAtUtc,
                UpdatedAtUtc = request.UpdatedAtUtc
            };
        }
    }

    public sealed class InMemoryTeamInviteRepository : ITeamInviteRepository
    {
        private readonly Dictionary<string, TeamInvite> byId = [];

        public Task<IReadOnlyList<TeamInvite>> GetByTeamAsync(string teamId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<TeamInvite>>(byId.Values
                .Where(item => string.Equals(item.TeamId, teamId, StringComparison.Ordinal))
                .Select(Clone)
                .ToList());
        }

        public Task<TeamInvite?> GetByTokenAsync(string token, string teamId, CancellationToken cancellationToken = default)
        {
            var invite = byId.Values.FirstOrDefault(item =>
                string.Equals(item.Token, token, StringComparison.Ordinal)
                && string.Equals(item.TeamId, teamId, StringComparison.Ordinal));

            return Task.FromResult(invite is null ? null : Clone(invite));
        }

        public Task UpsertAsync(TeamInvite invite, CancellationToken cancellationToken = default)
        {
            byId[invite.Id] = Clone(invite);
            return Task.CompletedTask;
        }

        private static TeamInvite Clone(TeamInvite invite)
        {
            return new TeamInvite
            {
                Id = invite.Id,
                TeamId = invite.TeamId,
                Email = invite.Email,
                InvitedBy = invite.InvitedBy,
                Token = invite.Token,
                Status = invite.Status,
                NeedsApproval = invite.NeedsApproval,
                ApprovedBy = invite.ApprovedBy,
                ExpiresAtUtc = invite.ExpiresAtUtc,
                CreatedAtUtc = invite.CreatedAtUtc,
                UpdatedAtUtc = invite.UpdatedAtUtc
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
