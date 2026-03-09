using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OTG.Api.Options;

namespace OTG.Api.Services;

public sealed class BlackbaudOAuthService(
    HttpClient httpClient,
    ILogger<BlackbaudOAuthService> logger,
    IOptions<BlackbaudOptions> blackbaudOptions) : IBlackbaudOAuthService
{
    public async Task<BlackbaudUserData> ExchangeCodeForUserAsync(string code, CancellationToken cancellationToken)
    {
        var options = blackbaudOptions.Value;
        EnsureConfigured(options);

        var token = await ExchangeAuthorizationCodeForTokenAsync(code, options, cancellationToken);
        var profile = await FetchCurrentSkyUserAsync(token.AccessToken, options.SubscriptionKey, cancellationToken);
        var merchants = await FetchMerchantAccountsAsync(token.AccessToken, options.PaymentsSubscriptionKey, cancellationToken);

        var email = GetString(profile, "email", "email_address");
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("Blackbaud profile response did not include an email address.");
        }

        return new BlackbaudUserData
        {
            Email = email.Trim().ToLowerInvariant(),
            BlackbaudId = GetString(profile, "id", "constituent_id"),
            FullName = GetString(profile, "name", "display_name"),
            FirstName = GetString(profile, "first_name", "firstname"),
            LastName = GetString(profile, "last_name", "lastname"),
            Title = GetString(profile, "title"),
            JobTitle = GetString(profile, "job_title"),
            Organization = GetString(profile, "organization", "organization_name"),
            Phone = GetString(profile, "phone", "phone_number"),
            Birthdate = ParseDate(GetString(profile, "birthdate", "birthday")),
            EnvironmentId = GetString(profile, "environment_id"),
            EnvironmentName = GetString(profile, "environment_name"),
            LegalEntityId = GetString(profile, "legal_entity_id"),
            LegalEntityName = GetString(profile, "legal_entity_name"),
            RefreshToken = token.RefreshToken,
            AccessTokenExpiresAtUtc = token.AccessTokenExpiresAtUtc,
            MerchantAccounts = merchants
        };
    }

    public async Task<BlackbaudTokenRefreshResult> RefreshAccessTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var options = blackbaudOptions.Value;
        EnsureConfigured(options);

        var token = await ExchangeRefreshTokenAsync(refreshToken, options, cancellationToken);
        return new BlackbaudTokenRefreshResult
        {
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken,
            AccessTokenExpiresAtUtc = token.AccessTokenExpiresAtUtc
        };
    }

    private async Task<TokenResponse> ExchangeAuthorizationCodeForTokenAsync(string code, BlackbaudOptions options, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.sky.blackbaud.com/token");
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.ApplicationId}:{options.ApplicationSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        request.Content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("redirect_uri", options.RedirectUri)
        ]);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Blackbaud token exchange failed with status {StatusCode}. Payload: {Payload}", (int)response.StatusCode, payload);
            throw new InvalidOperationException("Blackbaud token exchange failed.");
        }

        return await ParseTokenResponseAsync(response, cancellationToken);
    }

    private async Task<TokenResponse> ExchangeRefreshTokenAsync(string refreshToken, BlackbaudOptions options, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.sky.blackbaud.com/token");
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.ApplicationId}:{options.ApplicationSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        request.Content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("refresh_token", refreshToken)
        ]);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Blackbaud refresh token exchange failed with status {StatusCode}. Payload: {Payload}", (int)response.StatusCode, payload);
            throw new InvalidOperationException("Blackbaud refresh token exchange failed.");
        }

        return await ParseTokenResponseAsync(response, cancellationToken);
    }

    private static async Task<TokenResponse> ParseTokenResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var accessToken = GetString(document.RootElement, "access_token");
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("Blackbaud token response missing access_token.");
        }

        var refreshToken = GetString(document.RootElement, "refresh_token");
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new InvalidOperationException("Blackbaud token response missing refresh_token.");
        }

        var expiresIn = document.RootElement.TryGetProperty("expires_in", out var expiresValue) && expiresValue.TryGetInt32(out var seconds)
            ? seconds
            : (int?)null;

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpiresAtUtc = expiresIn.HasValue ? DateTimeOffset.UtcNow.AddSeconds(expiresIn.Value) : null
        };
    }

    private async Task<JsonElement> FetchCurrentSkyUserAsync(string accessToken, string subscriptionKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.sky.blackbaud.com/constituent/v1/constituents/currentskyuser");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Bb-Api-Subscription-Key", subscriptionKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Blackbaud profile fetch failed with status {StatusCode}. Payload: {Payload}", (int)response.StatusCode, payload);
            throw new InvalidOperationException("Failed to fetch Blackbaud profile.");
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement.Clone();
    }

    private async Task<IReadOnlyList<BlackbaudMerchantAccountData>> FetchMerchantAccountsAsync(string accessToken, string paymentsSubscriptionKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(paymentsSubscriptionKey))
        {
            return [];
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.sky.blackbaud.com/payments/v1/paymentconfigurations");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Bb-Api-Subscription-Key", paymentsSubscriptionKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var accounts = new List<BlackbaudMerchantAccountData>();
        if (!document.RootElement.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return accounts;
        }

        foreach (var item in value.EnumerateArray())
        {
            var merchantId = GetString(item, "merchant_id", "id");
            var name = GetString(item, "name", "merchant_name");
            if (string.IsNullOrWhiteSpace(merchantId) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            accounts.Add(new BlackbaudMerchantAccountData
            {
                Name = name,
                MerchantId = merchantId,
                Currency = GetString(item, "currency"),
                ProcessMode = GetString(item, "process_mode", "mode"),
                Active = item.TryGetProperty("active", out var active) && active.ValueKind is JsonValueKind.True
            });
        }

        return accounts;
    }

    private static void EnsureConfigured(BlackbaudOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApplicationId)
            || string.IsNullOrWhiteSpace(options.ApplicationSecret)
            || string.IsNullOrWhiteSpace(options.RedirectUri)
            || string.IsNullOrWhiteSpace(options.SubscriptionKey))
        {
            throw new InvalidOperationException("Blackbaud OAuth is not fully configured.");
        }
    }

    private static string? GetString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.Date
            : null;
    }

    private sealed class TokenResponse
    {
        public required string AccessToken { get; init; }
        public required string RefreshToken { get; init; }
        public DateTimeOffset? AccessTokenExpiresAtUtc { get; init; }
    }
}
