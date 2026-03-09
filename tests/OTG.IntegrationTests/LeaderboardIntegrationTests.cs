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

public sealed class LeaderboardIntegrationTests
{
    [Fact]
    public async Task LeaderboardTracks_GroupsByTrack_AndOrdersWithinTrack()
    {
        await using var factory = new TestApiFactory();
        var participant = factory.SeedUser("participant-track-rollup@example.com", UserRole.Participant);
        var author = factory.SeedUser("author-track-rollup@example.com", UserRole.Participant);
        var ideaA1 = factory.SeedIdea("hack-l-track-rollup", "idea-l-track-rollup-a1", author.Id, trackId: "track-a");
        var ideaA2 = factory.SeedIdea("hack-l-track-rollup", "idea-l-track-rollup-a2", author.Id, trackId: "track-a");
        var ideaB1 = factory.SeedIdea("hack-l-track-rollup", "idea-l-track-rollup-b1", author.Id, trackId: "track-b");

        factory.SeedRating(ideaA1.Id, "judge-a1", weightedScore: 90m, isModerated: false);
        factory.SeedRating(ideaA2.Id, "judge-a2", weightedScore: 70m, isModerated: false);
        factory.SeedRating(ideaB1.Id, "judge-b1", weightedScore: 80m, isModerated: false);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, participant.Id, participant.Email, "Participant");

        var response = await client.GetAsync($"/api/leaderboard/tracks?hackathonId={ideaA1.HackathonId}&sortMode=score");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<TrackRollupResponse>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload!.Tracks!.Count);

        var trackA = payload.Tracks!.First(track => string.Equals(track.TrackId, "track-a", StringComparison.Ordinal));
        Assert.Equal(2, trackA.Total);
        Assert.Equal("idea-l-track-rollup-a1", trackA.Items![0].IdeaId);
        Assert.Equal("idea-l-track-rollup-a2", trackA.Items![1].IdeaId);
        Assert.Equal(1, trackA.Items[0].Rank);
        Assert.Equal(0m, trackA.Items[0].DeltaFromTop);
        Assert.Equal(100m, trackA.Items[0].Percentile);
        Assert.Equal("platinum", trackA.Items[0].Band);
        Assert.Equal(2, trackA.Items[1].Rank);
        Assert.True(trackA.Items[1].DeltaFromTop > 0m);
        Assert.Equal(0m, trackA.Items[1].Percentile);
        Assert.Equal("bronze", trackA.Items[1].Band);
    }

    [Fact]
    public async Task LeaderboardTracks_AppliesPerTrackLimit_AndModerationVisibility()
    {
        await using var factory = new TestApiFactory();
        var participant = factory.SeedUser("participant-track-limit@example.com", UserRole.Participant);
        var admin = factory.SeedUser("admin-track-limit@example.com", UserRole.Admin);
        var author = factory.SeedUser("author-track-limit@example.com", UserRole.Participant);
        var ideaA1 = factory.SeedIdea("hack-l-track-limit", "idea-l-track-limit-a1", author.Id, trackId: "track-a");
        var ideaA2 = factory.SeedIdea("hack-l-track-limit", "idea-l-track-limit-a2", author.Id, trackId: "track-a");

        factory.SeedRating(ideaA1.Id, "judge-a1", weightedScore: 90m, isModerated: false);
        factory.SeedRating(ideaA2.Id, "judge-a2", weightedScore: 99m, isModerated: true);

        var participantClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(participantClient, participant.Id, participant.Email, "Participant");
        var participantResponse = await participantClient.GetAsync($"/api/leaderboard/tracks?hackathonId={ideaA1.HackathonId}&perTrackLimit=1");

        Assert.Equal(HttpStatusCode.OK, participantResponse.StatusCode);
        var participantPayload = await participantResponse.Content.ReadFromJsonAsync<TrackRollupResponse>();
        Assert.NotNull(participantPayload);
        var participantTrack = participantPayload!.Tracks!.First(track => string.Equals(track.TrackId, "track-a", StringComparison.Ordinal));
        Assert.Single(participantTrack.Items!);
        Assert.Equal("idea-l-track-limit-a1", participantTrack.Items![0].IdeaId);

        var adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(adminClient, admin.Id, admin.Email, "Admin");
        var adminResponse = await adminClient.GetAsync($"/api/leaderboard/tracks?hackathonId={ideaA1.HackathonId}&perTrackLimit=1");

        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
        var adminPayload = await adminResponse.Content.ReadFromJsonAsync<TrackRollupResponse>();
        Assert.NotNull(adminPayload);
        var adminTrack = adminPayload!.Tracks!.First(track => string.Equals(track.TrackId, "track-a", StringComparison.Ordinal));
        Assert.Single(adminTrack.Items!);
        Assert.Equal("idea-l-track-limit-a2", adminTrack.Items![0].IdeaId);
    }

    [Fact]
    public async Task Leaderboard_SortModeConfidence_PenalizesLowRatingCount()
    {
        await using var factory = new TestApiFactory();
        var participant = factory.SeedUser("participant-sort-confidence@example.com", UserRole.Participant);
        var author = factory.SeedUser("author-sort-confidence@example.com", UserRole.Participant);
        var ideaA = factory.SeedIdea("hack-l-sort-confidence", "idea-l-sort-confidence-a", author.Id);
        var ideaB = factory.SeedIdea("hack-l-sort-confidence", "idea-l-sort-confidence-b", author.Id);

        factory.SeedRating(ideaA.Id, "judge-a", weightedScore: 95m, isModerated: false);
        factory.SeedRating(ideaB.Id, "judge-b1", weightedScore: 80m, isModerated: false);
        factory.SeedRating(ideaB.Id, "judge-b2", weightedScore: 80m, isModerated: false);
        factory.SeedRating(ideaB.Id, "judge-b3", weightedScore: 80m, isModerated: false);
        factory.SeedRating(ideaB.Id, "judge-b4", weightedScore: 80m, isModerated: false);
        factory.SeedRating(ideaB.Id, "judge-b5", weightedScore: 80m, isModerated: false);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, participant.Id, participant.Email, "Participant");

        var response = await client.GetAsync($"/api/leaderboard?hackathonId={ideaA.HackathonId}&sortMode=confidence&confidencePivot=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LeaderboardResponse>();
        Assert.NotNull(payload);
        Assert.Equal("confidence", payload!.SortMode);
        Assert.Equal(5m, payload.ConfidencePivot);
        Assert.Equal(ideaB.Id, payload.Items![0].IdeaId);
        Assert.Equal(ideaA.Id, payload.Items[1].IdeaId);
    }

    [Fact]
    public async Task Leaderboard_ReturnsBadRequest_WhenConfidencePivotInvalid()
    {
        await using var factory = new TestApiFactory();
        var participant = factory.SeedUser("participant-confidence-invalid@example.com", UserRole.Participant);
        var author = factory.SeedUser("author-confidence-invalid@example.com", UserRole.Participant);
        var idea = factory.SeedIdea("hack-l-confidence-invalid", "idea-l-confidence-invalid", author.Id);
        factory.SeedRating(idea.Id, "judge-a", weightedScore: 72m, isModerated: false);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, participant.Id, participant.Email, "Participant");

        var response = await client.GetAsync($"/api/leaderboard?hackathonId={idea.HackathonId}&sortMode=confidence&confidencePivot=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Leaderboard_ReturnsNotModified_WhenIfNoneMatchMatches()
    {
        await using var factory = new TestApiFactory();
        var participant = factory.SeedUser("participant-etag@example.com", UserRole.Participant);
        var author = factory.SeedUser("author-etag@example.com", UserRole.Participant);
        var idea = factory.SeedIdea("hack-l-etag", "idea-l-etag", author.Id);
        factory.SeedRating(idea.Id, "judge-etag", weightedScore: 88m, isModerated: false);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, participant.Id, participant.Email, "Participant");

        var first = await client.GetAsync($"/api/leaderboard?hackathonId={idea.HackathonId}");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.True(first.Headers.TryGetValues("ETag", out var etagValues));
        var etag = etagValues!.Single();

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/leaderboard?hackathonId={idea.HackathonId}");
        request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        var second = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }

    [Fact]
    public async Task Leaderboard_SortModeCount_OrdersByRatingCount()
    {
        await using var factory = new TestApiFactory();
        var participant = factory.SeedUser("participant-sort-count@example.com", UserRole.Participant);
        var author = factory.SeedUser("author-sort-count@example.com", UserRole.Participant);
        var ideaA = factory.SeedIdea("hack-l-sort-count", "idea-l-sort-count-a", author.Id);
        var ideaB = factory.SeedIdea("hack-l-sort-count", "idea-l-sort-count-b", author.Id);

        factory.SeedRating(ideaA.Id, "judge-a1", weightedScore: 95m, isModerated: false);
        factory.SeedRating(ideaB.Id, "judge-b1", weightedScore: 60m, isModerated: false);
        factory.SeedRating(ideaB.Id, "judge-b2", weightedScore: 60m, isModerated: false);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, participant.Id, participant.Email, "Participant");

        var response = await client.GetAsync($"/api/leaderboard?hackathonId={ideaA.HackathonId}&sortMode=count");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LeaderboardResponse>();
        Assert.NotNull(payload);
        Assert.Equal("count", payload!.SortMode);
        Assert.Equal(ideaB.Id, payload.Items![0].IdeaId);
        Assert.Equal(ideaA.Id, payload.Items[1].IdeaId);
    }

    [Fact]
    public async Task Leaderboard_SortModeRecent_OrdersByLatestRatingUpdate()
    {
        await using var factory = new TestApiFactory();
        var participant = factory.SeedUser("participant-sort-recent@example.com", UserRole.Participant);
        var author = factory.SeedUser("author-sort-recent@example.com", UserRole.Participant);
        var ideaA = factory.SeedIdea("hack-l-sort-recent", "idea-l-sort-recent-a", author.Id);
        var ideaB = factory.SeedIdea("hack-l-sort-recent", "idea-l-sort-recent-b", author.Id);

        factory.SeedRating(ideaA.Id, "judge-a", weightedScore: 70m, isModerated: false, updatedAtUtc: DateTimeOffset.UtcNow.AddMinutes(-10));
        factory.SeedRating(ideaB.Id, "judge-b", weightedScore: 70m, isModerated: false, updatedAtUtc: DateTimeOffset.UtcNow.AddMinutes(-1));

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, participant.Id, participant.Email, "Participant");

        var response = await client.GetAsync($"/api/leaderboard?hackathonId={ideaA.HackathonId}&sortMode=recent");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LeaderboardResponse>();
        Assert.NotNull(payload);
        Assert.Equal("recent", payload!.SortMode);
        Assert.Equal(ideaB.Id, payload.Items![0].IdeaId);
        Assert.Equal(ideaA.Id, payload.Items[1].IdeaId);
    }

    [Fact]
    public async Task Leaderboard_ReturnsBadRequest_ForInvalidSortMode()
    {
        await using var factory = new TestApiFactory();
        var participant = factory.SeedUser("participant-sort-invalid@example.com", UserRole.Participant);
        var author = factory.SeedUser("author-sort-invalid@example.com", UserRole.Participant);
        var idea = factory.SeedIdea("hack-l-sort-invalid", "idea-l-sort-invalid", author.Id);
        factory.SeedRating(idea.Id, "judge-a", weightedScore: 70m, isModerated: false);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, participant.Id, participant.Email, "Participant");

        var response = await client.GetAsync($"/api/leaderboard?hackathonId={idea.HackathonId}&sortMode=unknown");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Leaderboard_UsesIdeaIdAsDeterministicTieBreaker()
    {
        await using var factory = new TestApiFactory();
        var participant = factory.SeedUser("participant-tie@example.com", UserRole.Participant);
        var author = factory.SeedUser("author-tie@example.com", UserRole.Participant);
        var ideaA = factory.SeedIdea("hack-l-tie", "idea-l-tie-a", author.Id);
        var ideaB = factory.SeedIdea("hack-l-tie", "idea-l-tie-b", author.Id);

        factory.SeedRating(ideaA.Id, "judge-a1", weightedScore: 80m, isModerated: false);
        factory.SeedRating(ideaA.Id, "judge-a2", weightedScore: 60m, isModerated: false);
        factory.SeedRating(ideaB.Id, "judge-b1", weightedScore: 80m, isModerated: false);
        factory.SeedRating(ideaB.Id, "judge-b2", weightedScore: 60m, isModerated: false);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, participant.Id, participant.Email, "Participant");

        var response = await client.GetAsync($"/api/leaderboard?hackathonId={ideaA.HackathonId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LeaderboardResponse>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload!.Items!.Count);
        Assert.Equal("idea-l-tie-a", payload.Items[0].IdeaId);
        Assert.Equal("idea-l-tie-b", payload.Items[1].IdeaId);
    }

    [Fact]
    public async Task Leaderboard_AppliesMinRatingCountThreshold()
    {
        await using var factory = new TestApiFactory();
        var participant = factory.SeedUser("participant-min-count@example.com", UserRole.Participant);
        var author = factory.SeedUser("author-min-count@example.com", UserRole.Participant);
        var ideaA = factory.SeedIdea("hack-l-min", "idea-l-min-a", author.Id);
        var ideaB = factory.SeedIdea("hack-l-min", "idea-l-min-b", author.Id);

        factory.SeedRating(ideaA.Id, "judge-a", weightedScore: 90m, isModerated: false);
        factory.SeedRating(ideaA.Id, "judge-b", weightedScore: 80m, isModerated: false);
        factory.SeedRating(ideaB.Id, "judge-c", weightedScore: 95m, isModerated: false);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, participant.Id, participant.Email, "Participant");

        var response = await client.GetAsync($"/api/leaderboard?hackathonId={ideaA.HackathonId}&minRatingCount=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LeaderboardResponse>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload!.MinRatingCount);
        Assert.Single(payload.Items!);
        Assert.Equal(ideaA.Id, payload.Items![0].IdeaId);
    }

    [Fact]
    public async Task Leaderboard_ReturnsBadRequest_WhenMinRatingCountInvalid()
    {
        await using var factory = new TestApiFactory();
        var participant = factory.SeedUser("participant-min-invalid@example.com", UserRole.Participant);
        var author = factory.SeedUser("author-min-invalid@example.com", UserRole.Participant);
        var idea = factory.SeedIdea("hack-l-min-invalid", "idea-l-min-invalid", author.Id);
        factory.SeedRating(idea.Id, "judge-a", weightedScore: 75m, isModerated: false);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, participant.Id, participant.Email, "Participant");

        var response = await client.GetAsync($"/api/leaderboard?hackathonId={idea.HackathonId}&minRatingCount=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Leaderboard_AppliesTrackFilter()
    {
        await using var factory = new TestApiFactory();
        var participant = factory.SeedUser("participant-track-filter@example.com", UserRole.Participant);
        var author = factory.SeedUser("author-track-filter@example.com", UserRole.Participant);
        var ideaA = factory.SeedIdea("hack-l-filter", "idea-l-filter-a", author.Id, trackId: "track-a");
        var ideaB = factory.SeedIdea("hack-l-filter", "idea-l-filter-b", author.Id, trackId: "track-b");

        factory.SeedRating(ideaA.Id, "judge-a", weightedScore: 70m, isModerated: false);
        factory.SeedRating(ideaB.Id, "judge-b", weightedScore: 90m, isModerated: false);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, participant.Id, participant.Email, "Participant");

        var response = await client.GetAsync($"/api/leaderboard?hackathonId={ideaA.HackathonId}&trackId=track-a");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LeaderboardResponse>();
        Assert.NotNull(payload);
        Assert.Single(payload!.Items!);
        Assert.Equal(ideaA.Id, payload.Items![0].IdeaId);
        Assert.Equal("track-a", payload.TrackId);
    }

    [Fact]
    public async Task Leaderboard_AppliesOffsetAndLimit()
    {
        await using var factory = new TestApiFactory();
        var participant = factory.SeedUser("participant-page@example.com", UserRole.Participant);
        var author = factory.SeedUser("author-page@example.com", UserRole.Participant);
        var ideaA = factory.SeedIdea("hack-l-page", "idea-l-page-a", author.Id);
        var ideaB = factory.SeedIdea("hack-l-page", "idea-l-page-b", author.Id);
        var ideaC = factory.SeedIdea("hack-l-page", "idea-l-page-c", author.Id);

        factory.SeedRating(ideaA.Id, "judge-a", weightedScore: 95m, isModerated: false);
        factory.SeedRating(ideaB.Id, "judge-b", weightedScore: 80m, isModerated: false);
        factory.SeedRating(ideaC.Id, "judge-c", weightedScore: 70m, isModerated: false);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, participant.Id, participant.Email, "Participant");

        var response = await client.GetAsync($"/api/leaderboard?hackathonId={ideaA.HackathonId}&offset=1&limit=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LeaderboardResponse>();
        Assert.NotNull(payload);
        Assert.Equal(3, payload!.Total);
        Assert.Equal(1, payload.Offset);
        Assert.Equal(1, payload.Limit);
        Assert.Single(payload.Items!);
        Assert.Equal(ideaB.Id, payload.Items[0].IdeaId);
    }

    [Fact]
    public async Task Leaderboard_ExcludesModeratedRatings_ForParticipant()
    {
        await using var factory = new TestApiFactory();
        var participant = factory.SeedUser("participant-leaderboard@example.com", UserRole.Participant);
        var author = factory.SeedUser("author-leaderboard@example.com", UserRole.Participant);
        var idea = factory.SeedIdea("hack-l-1", "idea-l-1", author.Id);

        factory.SeedRating(idea.Id, "judge-a", weightedScore: 80m, isModerated: false);
        factory.SeedRating(idea.Id, "judge-b", weightedScore: 20m, isModerated: true);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, participant.Id, participant.Email, "Participant");

        var response = await client.GetAsync($"/api/leaderboard?hackathonId={idea.HackathonId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LeaderboardResponse>();
        Assert.NotNull(payload);
        Assert.Single(payload!.Items!);
        Assert.Equal(1, payload.Items![0].RatingCount);
        Assert.Equal(80m, payload.Items[0].AverageWeightedScore);
    }

    [Fact]
    public async Task Leaderboard_IncludesModeratedRatings_ForAdmin()
    {
        await using var factory = new TestApiFactory();
        var admin = factory.SeedUser("admin-leaderboard@example.com", UserRole.Admin);
        var author = factory.SeedUser("author2-leaderboard@example.com", UserRole.Participant);
        var idea = factory.SeedIdea("hack-l-2", "idea-l-2", author.Id);

        factory.SeedRating(idea.Id, "judge-a", weightedScore: 80m, isModerated: false);
        factory.SeedRating(idea.Id, "judge-b", weightedScore: 20m, isModerated: true);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, admin.Id, admin.Email, "Admin");

        var response = await client.GetAsync($"/api/leaderboard?hackathonId={idea.HackathonId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LeaderboardResponse>();
        Assert.NotNull(payload);
        Assert.Single(payload!.Items!);
        Assert.Equal(2, payload.Items![0].RatingCount);
        Assert.Equal(50m, payload.Items[0].AverageWeightedScore);
    }

    [Fact]
    public async Task Leaderboard_ReturnsOrderedByAverageScoreDescending()
    {
        await using var factory = new TestApiFactory();
        var participant = factory.SeedUser("participant-order@example.com", UserRole.Participant);
        var author = factory.SeedUser("author-order@example.com", UserRole.Participant);
        var ideaA = factory.SeedIdea("hack-l-3", "idea-l-3a", author.Id);
        var ideaB = factory.SeedIdea("hack-l-3", "idea-l-3b", author.Id);

        factory.SeedRating(ideaA.Id, "judge-a", weightedScore: 60m, isModerated: false);
        factory.SeedRating(ideaB.Id, "judge-b", weightedScore: 90m, isModerated: false);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, participant.Id, participant.Email, "Participant");

        var response = await client.GetAsync($"/api/leaderboard?hackathonId={ideaA.HackathonId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LeaderboardResponse>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload!.Items!.Count);
        Assert.Equal(ideaB.Id, payload.Items[0].IdeaId);
        Assert.Equal(ideaA.Id, payload.Items[1].IdeaId);
        Assert.Equal(1, payload.Items[0].Rank);
        Assert.Equal(0m, payload.Items[0].DeltaFromTop);
        Assert.Equal(100m, payload.Items[0].Percentile);
        Assert.Equal("platinum", payload.Items[0].Band);
        Assert.Equal(2, payload.Items[1].Rank);
        Assert.Equal(30m, payload.Items[1].DeltaFromTop);
        Assert.Equal(0m, payload.Items[1].Percentile);
        Assert.Equal("bronze", payload.Items[1].Band);
    }

    [Fact]
    public async Task Leaderboard_UsesHackathonConfiguredBandThresholds()
    {
        await using var factory = new TestApiFactory();
        var participant = factory.SeedUser("participant-configured-bands@example.com", UserRole.Participant);
        var author = factory.SeedUser("author-configured-bands@example.com", UserRole.Participant);
        factory.SeedHackathonBandSettings("hack-l-configured-bands", platinumMinPercentile: 95m, goldMinPercentile: 60m, silverMinPercentile: 30m);
        var topIdea = factory.SeedIdea("hack-l-configured-bands", "idea-l-configured-bands-top", author.Id);
        var secondIdea = factory.SeedIdea("hack-l-configured-bands", "idea-l-configured-bands-second", author.Id);
        var thirdIdea = factory.SeedIdea("hack-l-configured-bands", "idea-l-configured-bands-third", author.Id);
        var fourthIdea = factory.SeedIdea("hack-l-configured-bands", "idea-l-configured-bands-fourth", author.Id);

        factory.SeedRating(topIdea.Id, "judge-top", weightedScore: 100m, isModerated: false);
        factory.SeedRating(secondIdea.Id, "judge-second", weightedScore: 90m, isModerated: false);
        factory.SeedRating(thirdIdea.Id, "judge-third", weightedScore: 80m, isModerated: false);
        factory.SeedRating(fourthIdea.Id, "judge-fourth", weightedScore: 70m, isModerated: false);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddAuthHeaders(client, participant.Id, participant.Email, "Participant");

        var response = await client.GetAsync("/api/leaderboard?hackathonId=hack-l-configured-bands");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LeaderboardResponse>();
        Assert.NotNull(payload);
        Assert.NotNull(payload!.BandThresholds);
        Assert.Equal(95m, payload.BandThresholds!.PlatinumMinPercentile);
        Assert.Equal(60m, payload.BandThresholds.GoldMinPercentile);
        Assert.Equal(30m, payload.BandThresholds.SilverMinPercentile);
        Assert.Equal(["platinum", "gold", "silver", "bronze"], payload.Items!.Select(item => item.Band).ToArray());
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

    private sealed class LeaderboardResponse
    {
        public string? HackathonId { get; set; }
        public string? TrackId { get; set; }
        public int MinRatingCount { get; set; }
        public string? SortMode { get; set; }
        public decimal ConfidencePivot { get; set; }
        public BandThresholdsResponse? BandThresholds { get; set; }
        public string? ETag { get; set; }
        public int Total { get; set; }
        public int Offset { get; set; }
        public int Limit { get; set; }
        public List<LeaderboardItem>? Items { get; set; }
    }

    private sealed class LeaderboardItem
    {
        public string? IdeaId { get; set; }
        public int RatingCount { get; set; }
        public decimal AverageWeightedScore { get; set; }
        public int Rank { get; set; }
        public decimal DeltaFromTop { get; set; }
        public decimal Percentile { get; set; }
        public string? Band { get; set; }
    }

    private sealed class TrackRollupResponse
    {
        public string? HackathonId { get; set; }
        public int MinRatingCount { get; set; }
        public string? SortMode { get; set; }
        public decimal ConfidencePivot { get; set; }
        public BandThresholdsResponse? BandThresholds { get; set; }
        public int PerTrackLimit { get; set; }
        public List<TrackRollupItem>? Tracks { get; set; }
    }

    private sealed class BandThresholdsResponse
    {
        public decimal PlatinumMinPercentile { get; set; }
        public decimal GoldMinPercentile { get; set; }
        public decimal SilverMinPercentile { get; set; }
    }

    private sealed class TrackRollupItem
    {
        public string? TrackId { get; set; }
        public int Total { get; set; }
        public List<LeaderboardItem>? Items { get; set; }
    }

    private sealed class TestApiFactory : WebApplicationFactory<OTG.Api.Program>
    {
        public InMemoryUserRepository UserRepository { get; } = new();
        public InMemoryIdeaRepository IdeaRepository { get; } = new();
        public InMemoryRatingRepository RatingRepository { get; } = new();
        public InMemoryHackathonRepository HackathonRepository { get; } = new();

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

                services.AddSingleton<IUserRepository>(UserRepository);
                services.AddSingleton<IIdeaRepository>(IdeaRepository);
                services.AddSingleton<IRatingRepository>(RatingRepository);
                services.AddSingleton<IHackathonRepository>(HackathonRepository);
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

        public Idea SeedIdea(string hackathonId, string ideaId, string authorId, string? trackId = null)
        {
            var idea = new Idea
            {
                Id = ideaId,
                HackathonId = hackathonId,
                AuthorId = authorId,
                Title = ideaId,
                Description = "desc",
                Status = IdeaStatus.Submitted,
                TrackId = trackId,
                TermsAccepted = true
            };

            IdeaRepository.UpsertAsync(idea).GetAwaiter().GetResult();
            return idea;
        }

        public IdeaRating SeedRating(string ideaId, string judgeId, decimal weightedScore, bool isModerated, DateTimeOffset? updatedAtUtc = null)
        {
            var rating = new IdeaRating
            {
                Id = Guid.NewGuid().ToString("N"),
                IdeaId = ideaId,
                JudgeId = judgeId,
                WeightedScore = weightedScore,
                Scores = [new CriterionScore { CriterionId = "impact", Score = 4 }],
                IsModerated = isModerated,
                ModerationReason = isModerated ? "Moderated" : null,
                ModeratedBy = isModerated ? Guid.NewGuid().ToString("N") : null,
                ModeratedAtUtc = isModerated ? DateTimeOffset.UtcNow : null,
                UpdatedAtUtc = updatedAtUtc ?? DateTimeOffset.UtcNow
            };

            RatingRepository.UpsertAsync(rating).GetAwaiter().GetResult();
            return rating;
        }

        public void SeedHackathonBandSettings(string hackathonId, decimal platinumMinPercentile, decimal goldMinPercentile, decimal silverMinPercentile)
        {
            HackathonRepository.UpsertAsync(new Hackathon
            {
                Id = hackathonId,
                HackathonId = hackathonId,
                Name = hackathonId,
                LeaderboardBands = new LeaderboardBandSettings
                {
                    PlatinumMinPercentile = platinumMinPercentile,
                    GoldMinPercentile = goldMinPercentile,
                    SilverMinPercentile = silverMinPercentile
                }
            }).GetAwaiter().GetResult();
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

    private sealed class InMemoryRatingRepository : IRatingRepository
    {
        private readonly List<IdeaRating> ratings = [];

        public Task<IReadOnlyList<IdeaRating>> GetByIdeaAsync(string ideaId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<IdeaRating>>(ratings
                .Where(item => string.Equals(item.IdeaId, ideaId, StringComparison.Ordinal))
                .Select(Clone)
                .ToList());
        }

        public Task UpsertAsync(IdeaRating rating, CancellationToken cancellationToken = default)
        {
            var index = ratings.FindIndex(item => string.Equals(item.Id, rating.Id, StringComparison.Ordinal));
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
                LogoUrl = hackathon.LogoUrl,
                LedeImageUrl = hackathon.LedeImageUrl,
                RegistrationOpen = hackathon.RegistrationOpen,
                SubmissionDeadline = hackathon.SubmissionDeadline,
                JudgingStart = hackathon.JudgingStart,
                JudgingEnd = hackathon.JudgingEnd,
                RulesMarkdown = hackathon.RulesMarkdown,
                Faq = hackathon.Faq.Select(item => new FaqEntry { Question = item.Question, Answer = item.Answer }).ToList(),
                Terms = hackathon.Terms,
                SwagHtml = hackathon.SwagHtml,
                Tracks = hackathon.Tracks.ToList(),
                Awards = hackathon.Awards.ToList(),
                JudgingCriteria = hackathon.JudgingCriteria.ToList(),
                Milestones = hackathon.Milestones.ToList(),
                LeaderboardBands = hackathon.LeaderboardBands is null
                    ? null
                    : new LeaderboardBandSettings
                    {
                        PlatinumMinPercentile = hackathon.LeaderboardBands.PlatinumMinPercentile,
                        GoldMinPercentile = hackathon.LeaderboardBands.GoldMinPercentile,
                        SilverMinPercentile = hackathon.LeaderboardBands.SilverMinPercentile
                    },
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
