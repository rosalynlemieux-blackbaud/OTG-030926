namespace OTG.Api.Services;

public interface IBlackbaudStateStore
{
    void Store(string state, string? origin, TimeSpan ttl);
    bool TryConsume(string state, out string? origin);
}
