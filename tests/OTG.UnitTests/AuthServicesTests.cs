using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using OTG.Api.Services;
using System.Threading;

namespace OTG.UnitTests;

public sealed class AuthServicesTests
{
    [Fact]
    public void PasswordHasher_VerifyPassword_Succeeds_ForCorrectPassword()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var hash = hasher.HashPassword("Sup3r$afePass!");

        hasher.VerifyPassword("Sup3r$afePass!", hash).Should().BeTrue();
    }

    [Fact]
    public void PasswordHasher_VerifyPassword_Fails_ForIncorrectPassword()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var hash = hasher.HashPassword("Sup3r$afePass!");

        hasher.VerifyPassword("wrong-password", hash).Should().BeFalse();
    }

    [Fact]
    public void BlackbaudStateStore_Consumes_State_Only_Once()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new BlackbaudStateStore(cache);

        store.Store("state-1", "https://app.example.com", TimeSpan.FromMinutes(5));

        var firstConsume = store.TryConsume("state-1", out var origin);
        var secondConsume = store.TryConsume("state-1", out _);

        firstConsume.Should().BeTrue();
        origin.Should().Be("https://app.example.com");
        secondConsume.Should().BeFalse();
    }

    [Fact]
    public async Task BlackbaudStateStore_Consumes_State_Once_Under_Concurrency()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new BlackbaudStateStore(cache);
        store.Store("state-concurrent", "https://app.example.com", TimeSpan.FromMinutes(5));

        var successCount = 0;
        var tasks = Enumerable.Range(0, 20)
            .Select(i => Task.Run(() =>
            {
                if (store.TryConsume("state-concurrent", out _))
                {
                    Interlocked.Increment(ref successCount);
                }
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        successCount.Should().Be(1);
    }
}
