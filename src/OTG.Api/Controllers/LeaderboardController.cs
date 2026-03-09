using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using OTG.Application.Abstractions.Repositories;
using OTG.Domain.Ideas;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OTG.Api.Controllers;

[ApiController]
[Route("api/leaderboard")]
[Authorize(Policy = "NotBanned")]
public sealed class LeaderboardController(
    IIdeaRepository ideaRepository,
    IRatingRepository ratingRepository,
    IMemoryCache cache) : ControllerBase
{
    [HttpGet("tracks")]
    public async Task<ActionResult<object>> GetTrackRollup(
        [FromQuery] string hackathonId,
        [FromQuery] int minRatingCount = 1,
        [FromQuery] string sortMode = "score",
        [FromQuery] decimal confidencePivot = 5m,
        [FromQuery] int perTrackLimit = 10,
        CancellationToken cancellationToken = default)
    {
        var normalizedSortMode = sortMode.Trim().ToLowerInvariant();
        if (normalizedSortMode is not ("score" or "count" or "recent" or "confidence"))
        {
            return BadRequest("sortMode must be one of: score, count, recent, confidence.");
        }

        if (confidencePivot <= 0)
        {
            return BadRequest("confidencePivot must be greater than 0.");
        }

        if (minRatingCount < 1)
        {
            return BadRequest("minRatingCount must be at least 1.");
        }

        if (perTrackLimit < 1 || perTrackLimit > 100)
        {
            return BadRequest("perTrackLimit must be between 1 and 100.");
        }

        var ideas = await ideaRepository.SearchAsync(hackathonId, null, null, null, cancellationToken);
        var includeModerated = User.IsInRole("Admin");
        var leaderboard = await BuildLeaderboardItemsAsync(ideas, includeModerated, minRatingCount, confidencePivot, cancellationToken);
        var ordered = SortLeaderboardItems(leaderboard, normalizedSortMode);
        var annotated = AnnotateRankAndDelta(ordered, normalizedSortMode);

        var tracks = annotated
            .GroupBy(item => item.TrackId ?? string.Empty, StringComparer.Ordinal)
            .Select(group => new
            {
                TrackId = string.IsNullOrWhiteSpace(group.Key) ? null : group.Key,
                Total = group.Count(),
                Items = AnnotateRankAndDelta(group.ToList(), normalizedSortMode).Take(perTrackLimit).ToList()
            })
            .OrderBy(item => item.TrackId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Ok(new
        {
            HackathonId = hackathonId,
            MinRatingCount = minRatingCount,
            SortMode = normalizedSortMode,
            ConfidencePivot = confidencePivot,
            PerTrackLimit = perTrackLimit,
            Tracks = tracks
        });
    }

    [HttpGet]
    public async Task<ActionResult<object>> Get(
        [FromQuery] string hackathonId,
        [FromQuery] string? trackId,
        [FromQuery] int minRatingCount = 1,
        [FromQuery] string sortMode = "score",
        [FromQuery] decimal confidencePivot = 5m,
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var normalizedSortMode = sortMode.Trim().ToLowerInvariant();
        if (normalizedSortMode is not ("score" or "count" or "recent" or "confidence"))
        {
            return BadRequest("sortMode must be one of: score, count, recent, confidence.");
        }

        if (confidencePivot <= 0)
        {
            return BadRequest("confidencePivot must be greater than 0.");
        }

        if (minRatingCount < 1)
        {
            return BadRequest("minRatingCount must be at least 1.");
        }

        if (offset < 0)
        {
            return BadRequest("Offset must be zero or greater.");
        }

        if (limit < 1 || limit > 100)
        {
            return BadRequest("Limit must be between 1 and 100.");
        }

        var ideas = await ideaRepository.SearchAsync(hackathonId, null, trackId, null, cancellationToken);
        var includeModerated = User.IsInRole("Admin");
        var cacheKey = BuildCacheKey(hackathonId, trackId, minRatingCount, normalizedSortMode, confidencePivot, includeModerated, offset, limit);

        if (!cache.TryGetValue(cacheKey, out LeaderboardResponsePayload? payload))
        {
            var leaderboard = await BuildLeaderboardItemsAsync(ideas, includeModerated, minRatingCount, confidencePivot, cancellationToken);
            var ordered = SortLeaderboardItems(leaderboard, normalizedSortMode);
            var annotated = AnnotateRankAndDelta(ordered, normalizedSortMode);

            var paged = annotated
                .Skip(offset)
                .Take(limit)
                .ToList();

            payload = new LeaderboardResponsePayload
            {
                HackathonId = hackathonId,
                TrackId = trackId,
                MinRatingCount = minRatingCount,
                SortMode = normalizedSortMode,
                ConfidencePivot = confidencePivot,
                Total = annotated.Count,
                Offset = offset,
                Limit = limit,
                Items = paged,
                ETag = BuildEtag(hackathonId, trackId, minRatingCount, normalizedSortMode, confidencePivot, offset, limit, paged)
            };

            cache.Set(cacheKey, payload, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
            });
        }

        if (TryMatchIfNoneMatch(payload!.ETag))
        {
            Response.Headers.ETag = payload.ETag;
            Response.Headers.CacheControl = "private, max-age=30";
            return StatusCode(StatusCodes.Status304NotModified);
        }

        Response.Headers.ETag = payload.ETag;
        Response.Headers.CacheControl = "private, max-age=30";

        return Ok(new
        {
            payload.HackathonId,
            payload.TrackId,
            payload.MinRatingCount,
            payload.SortMode,
            payload.ConfidencePivot,
            payload.Total,
            payload.Offset,
            payload.Limit,
            payload.Items
        });
    }

    private async Task<List<LeaderboardItem>> BuildLeaderboardItemsAsync(
        IReadOnlyList<Idea> ideas,
        bool includeModerated,
        int minRatingCount,
        decimal confidencePivot,
        CancellationToken cancellationToken)
    {
        var leaderboard = new List<LeaderboardItem>();
        foreach (var idea in ideas)
        {
            var ratings = await ratingRepository.GetByIdeaAsync(idea.Id, cancellationToken);
            var visibleRatings = includeModerated
                ? ratings
                : ratings.Where(rating => !rating.IsModerated).ToList();

            if (visibleRatings.Count == 0 || visibleRatings.Count < minRatingCount)
            {
                continue;
            }

            var averageScore = Math.Round(visibleRatings.Average(rating => rating.WeightedScore), 2, MidpointRounding.AwayFromZero);
            var confidenceScore = Math.Round(averageScore * ((decimal)visibleRatings.Count / ((decimal)visibleRatings.Count + confidencePivot)), 2, MidpointRounding.AwayFromZero);
            leaderboard.Add(new LeaderboardItem
            {
                IdeaId = idea.Id,
                Title = idea.Title,
                TrackId = idea.TrackId,
                TeamId = idea.TeamId,
                RatingCount = visibleRatings.Count,
                AverageWeightedScore = averageScore,
                ConfidenceWeightedScore = confidenceScore,
                LatestRatingUpdatedAt = visibleRatings.Max(rating => rating.UpdatedAtUtc)
            });
        }

        return leaderboard;
    }

    private static List<LeaderboardItem> SortLeaderboardItems(List<LeaderboardItem> leaderboard, string normalizedSortMode)
    {
        return normalizedSortMode switch
        {
            "count" => leaderboard
                .OrderByDescending(item => item.RatingCount)
                .ThenByDescending(item => item.AverageWeightedScore)
                .ThenBy(item => item.IdeaId, StringComparer.Ordinal)
                .ToList(),
            "recent" => leaderboard
                .OrderByDescending(item => item.LatestRatingUpdatedAt)
                .ThenByDescending(item => item.AverageWeightedScore)
                .ThenByDescending(item => item.RatingCount)
                .ThenBy(item => item.IdeaId, StringComparer.Ordinal)
                .ToList(),
            "confidence" => leaderboard
                .OrderByDescending(item => item.ConfidenceWeightedScore)
                .ThenByDescending(item => item.RatingCount)
                .ThenByDescending(item => item.AverageWeightedScore)
                .ThenBy(item => item.IdeaId, StringComparer.Ordinal)
                .ToList(),
            _ => leaderboard
                .OrderByDescending(item => item.AverageWeightedScore)
                .ThenByDescending(item => item.RatingCount)
                .ThenBy(item => item.IdeaId, StringComparer.Ordinal)
                .ToList()
        };
    }

    private static List<LeaderboardItem> AnnotateRankAndDelta(List<LeaderboardItem> ordered, string normalizedSortMode)
    {
        if (ordered.Count == 0)
        {
            return ordered;
        }

        var topMetric = GetModeMetric(ordered[0], normalizedSortMode);
        for (var index = 0; index < ordered.Count; index++)
        {
            var currentMetric = GetModeMetric(ordered[index], normalizedSortMode);
            ordered[index].Rank = index + 1;
            ordered[index].DeltaFromTop = Math.Round(topMetric - currentMetric, 2, MidpointRounding.AwayFromZero);
            ordered[index].Percentile = CalculatePercentile(index + 1, ordered.Count);
            ordered[index].Band = CalculateBand(ordered[index].Percentile);
        }

        return ordered;
    }

    private static string CalculateBand(decimal percentile)
    {
        return percentile switch
        {
            >= 90m => "platinum",
            >= 75m => "gold",
            >= 50m => "silver",
            _ => "bronze"
        };
    }

    private static decimal CalculatePercentile(int rank, int total)
    {
        if (total <= 1)
        {
            return 100m;
        }

        var percentile = 100m * (total - rank) / (total - 1);
        return Math.Round(percentile, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal GetModeMetric(LeaderboardItem item, string normalizedSortMode)
    {
        return normalizedSortMode switch
        {
            "count" => item.RatingCount,
            "recent" => item.LatestRatingUpdatedAt.ToUnixTimeMilliseconds(),
            "confidence" => item.ConfidenceWeightedScore,
            _ => item.AverageWeightedScore
        };
    }

    private static string BuildCacheKey(string hackathonId, string? trackId, int minRatingCount, string sortMode, decimal confidencePivot, bool includeModerated, int offset, int limit)
        => $"leaderboard:{hackathonId}:{trackId ?? "*"}:{minRatingCount}:{sortMode}:{confidencePivot.ToString(CultureInfo.InvariantCulture)}:{includeModerated}:{offset}:{limit}";

    private bool TryMatchIfNoneMatch(string etag)
    {
        if (!Request.Headers.TryGetValue("If-None-Match", out var values))
        {
            return false;
        }

        var candidates = values.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return candidates.Any(candidate => string.Equals(candidate, etag, StringComparison.Ordinal) || candidate == "*");
    }

    private static string BuildEtag(string hackathonId, string? trackId, int minRatingCount, string sortMode, decimal confidencePivot, int offset, int limit, IReadOnlyList<LeaderboardItem> items)
    {
        var canonical = new StringBuilder();
        canonical.Append(hackathonId).Append('|')
            .Append(trackId ?? string.Empty).Append('|')
            .Append(minRatingCount).Append('|')
            .Append(sortMode).Append('|')
            .Append(confidencePivot.ToString(CultureInfo.InvariantCulture)).Append('|')
            .Append(offset).Append('|')
            .Append(limit);

        foreach (var item in items)
        {
            canonical.Append('|')
                .Append(item.IdeaId)
                .Append(':')
                .Append(item.RatingCount)
                .Append(':')
                .Append(item.AverageWeightedScore)
                .Append(':')
                .Append(item.ConfidenceWeightedScore)
                .Append(':')
                .Append(item.LatestRatingUpdatedAt.ToUnixTimeMilliseconds());
        }

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        return $"\"{Convert.ToHexString(hashBytes)}\"";
    }

    private sealed class LeaderboardResponsePayload
    {
        public required string HackathonId { get; init; }
        public string? TrackId { get; init; }
        public int MinRatingCount { get; init; }
        public required string SortMode { get; init; }
        public decimal ConfidencePivot { get; init; }
        public int Total { get; init; }
        public int Offset { get; init; }
        public int Limit { get; init; }
        public required List<LeaderboardItem> Items { get; init; }
        public required string ETag { get; init; }
    }

    private sealed class LeaderboardItem
    {
        public required string IdeaId { get; init; }
        public required string Title { get; init; }
        public string? TrackId { get; init; }
        public string? TeamId { get; init; }
        public int RatingCount { get; init; }
        public decimal AverageWeightedScore { get; init; }
        public decimal ConfidenceWeightedScore { get; init; }
        public DateTimeOffset LatestRatingUpdatedAt { get; init; }
        public int Rank { get; set; }
        public decimal DeltaFromTop { get; set; }
        public decimal Percentile { get; set; }
        public string Band { get; set; } = "bronze";
    }
}
