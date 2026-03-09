using Microsoft.Extensions.Caching.Memory;

namespace OTG.Api.Services;

public sealed class SparkIdeaService(IMemoryCache memoryCache) : ISparkIdeaService
{
    public SparkIdeaResult Generate(string conversationId, string message)
    {
        var count = memoryCache.GetOrCreate($"spark:{conversationId}:count", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            return 0;
        });

        var nextCount = count + 1;
        memoryCache.Set($"spark:{conversationId}:count", nextCount, TimeSpan.FromMinutes(30));

        if (nextCount < 3)
        {
            return new SparkIdeaResult
            {
                Reply = $"Great start. Tell me more about the user problem or impact for: \"{message.Trim()}\"",
                ReadyToSubmit = false
            };
        }

        var compact = string.Join(' ', message.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(6));
        var title = $"Idea: {compact}".Trim();
        var description =
            $"Problem statement and concept summary based on brainstorming: {message.Trim()}\n\n" +
            "Proposed approach:\n- Define target users\n- Build MVP workflow\n- Measure value with success metrics";

        return new SparkIdeaResult
        {
            Reply = "Great—this concept is structured enough to start as a draft idea.",
            ReadyToSubmit = true,
            Title = title.Length > 120 ? title[..120] : title,
            Description = description
        };
    }
}
