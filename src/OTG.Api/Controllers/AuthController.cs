using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OTG.Api.Contracts;
using OTG.Api.Extensions;
using OTG.Api.Options;
using OTG.Api.Services;
using OTG.Application.Abstractions.Repositories;
using OTG.Domain.Identity;

namespace OTG.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IBlackbaudOAuthService blackbaudOAuthService,
    IOptions<BlackbaudOptions> blackbaudOptions,
    ILogger<AuthController> logger,
    IBlackbaudStateStore blackbaudStateStore) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var existing = await userRepository.GetByEmailAsync(email, cancellationToken);
        if (existing is not null)
        {
            return Conflict("A user with this email already exists.");
        }

        var userId = Guid.NewGuid().ToString("N");
        var user = new User
        {
            Id = userId,
            Email = email,
            PasswordHash = passwordHasher.HashPassword(request.Password),
            EmailConfirmed = false,
            Roles = [UserRole.Participant],
            Profile = new Profile
            {
                Id = Guid.NewGuid().ToString("N"),
                UserId = userId,
                Email = email,
                Name = email.Split('@')[0]
            }
        };

        await userRepository.UpsertAsync(user, cancellationToken);

        return Ok(ToResponse(user));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await userRepository.GetByEmailAsync(email, cancellationToken);
        if (user is null || string.IsNullOrWhiteSpace(user.PasswordHash) || !passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Unauthorized();
        }

        return Ok(ToResponse(user));
    }

    [HttpGet("blackbaud/start")]
    [AllowAnonymous]
    public ActionResult<BlackbaudStartResponse> StartBlackbaudSignIn([FromQuery] string? origin)
    {
        var options = blackbaudOptions.Value;
        if (string.IsNullOrWhiteSpace(options.ApplicationId) || string.IsNullOrWhiteSpace(options.RedirectUri))
        {
            return BadRequest("Blackbaud OAuth is not configured.");
        }

        if (!IsAllowedOrigin(origin))
        {
            return BadRequest("Origin must be an absolute http/https URL.");
        }

        var state = Guid.NewGuid().ToString("N");
        blackbaudStateStore.Store(state, origin, TimeSpan.FromMinutes(10));
        var authorizationUrl = QueryHelpers.AddQueryString(
            "https://app.blackbaud.com/oauth/authorize",
            new Dictionary<string, string?>
            {
                ["client_id"] = options.ApplicationId,
                ["response_type"] = "code",
                ["redirect_uri"] = options.RedirectUri,
                ["state"] = state
            });

        return Ok(new BlackbaudStartResponse
        {
            AuthorizationUrl = authorizationUrl,
            State = state
        });
    }

    [HttpGet("blackbaud/callback")]
    [AllowAnonymous]
    public async Task<ActionResult> BlackbaudCallback([FromQuery] BlackbaudCallbackRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest("Missing OAuth authorization code.");
        }

        if (string.IsNullOrWhiteSpace(request.State) || !blackbaudStateStore.TryConsume(request.State, out var origin))
        {
            return BadRequest("Invalid or expired OAuth state.");
        }

        BlackbaudUserData blackbaudData;
        try
        {
            blackbaudData = await blackbaudOAuthService.ExchangeCodeForUserAsync(request.Code, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Blackbaud OAuth callback processing failed.");
            return Problem(
                title: "Blackbaud token exchange failed",
                detail: "Unable to complete Blackbaud sign-in at this time.",
                statusCode: StatusCodes.Status502BadGateway);
        }

        var user = await userRepository.GetByEmailAsync(blackbaudData.Email, cancellationToken);
        if (user is null)
        {
            var userId = Guid.NewGuid().ToString("N");
            user = new User
            {
                Id = userId,
                Email = blackbaudData.Email,
                EmailConfirmed = true,
                Roles = [UserRole.Participant],
                Profile = new Profile
                {
                    Id = Guid.NewGuid().ToString("N"),
                    UserId = userId,
                    Email = blackbaudData.Email
                }
            };
        }

        user.Profile ??= new Profile
        {
            Id = Guid.NewGuid().ToString("N"),
            UserId = user.Id,
            Email = user.Email
        };

        user.Email = blackbaudData.Email;
        user.EmailConfirmed = true;
        user.Profile.Email = blackbaudData.Email;
        user.Profile.Name = blackbaudData.FullName ?? user.Profile.Name;
        user.Profile.BlackbaudLinked = true;
        user.Profile.BlackbaudId = blackbaudData.BlackbaudId;
        user.Profile.FirstName = blackbaudData.FirstName;
        user.Profile.LastName = blackbaudData.LastName;
        user.Profile.Title = blackbaudData.Title;
        user.Profile.JobTitle = blackbaudData.JobTitle;
        user.Profile.Organization = blackbaudData.Organization;
        user.Profile.Phone = blackbaudData.Phone;
        user.Profile.Birthdate = blackbaudData.Birthdate;
        user.Profile.EnvironmentId = blackbaudData.EnvironmentId;
        user.Profile.EnvironmentName = blackbaudData.EnvironmentName;
        user.Profile.LegalEntityId = blackbaudData.LegalEntityId;
        user.Profile.LegalEntityName = blackbaudData.LegalEntityName;
        user.Profile.BlackbaudRefreshToken = blackbaudData.RefreshToken;
        user.Profile.BlackbaudRefreshTokenUpdatedAtUtc = DateTimeOffset.UtcNow;
        user.Profile.BlackbaudAccessTokenExpiresAtUtc = blackbaudData.AccessTokenExpiresAtUtc;
        user.Profile.MerchantAccounts = blackbaudData.MerchantAccounts
            .Select(account => new MerchantAccount
            {
                Name = account.Name,
                MerchantId = account.MerchantId,
                Currency = account.Currency,
                ProcessMode = account.ProcessMode,
                Active = account.Active
            })
            .ToList();
        user.Profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
        user.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await userRepository.UpsertAsync(user, cancellationToken);

        var token = tokenService.CreateAccessToken(user);
        Response.Cookies.Append(
            "otg_access_token",
            token,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddHours(8),
                Path = "/"
            });

        var redirectBase = !string.IsNullOrWhiteSpace(origin) && IsAllowedOrigin(origin)
            ? origin
            : $"{Request.Scheme}://{Request.Host}";
        var redirectUrl = QueryHelpers.AddQueryString(
            redirectBase!,
            new Dictionary<string, string?>
            {
                ["provider"] = "blackbaud"
            });

        return Redirect(redirectUrl);
    }

    [HttpPost("blackbaud/refresh")]
    [Authorize(Policy = "NotBanned")]
    public async Task<ActionResult<object>> RefreshBlackbaudToken(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user?.Profile is null || !user.Profile.BlackbaudLinked)
        {
            return BadRequest("Current user is not linked to Blackbaud.");
        }

        if (string.IsNullOrWhiteSpace(user.Profile.BlackbaudRefreshToken))
        {
            return BadRequest("No Blackbaud refresh token is available.");
        }

        BlackbaudTokenRefreshResult refreshResult;
        try
        {
            refreshResult = await blackbaudOAuthService.RefreshAccessTokenAsync(user.Profile.BlackbaudRefreshToken, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Blackbaud refresh token processing failed for user {UserId}.", user.Id);
            return Problem(
                title: "Blackbaud refresh failed",
                detail: "Unable to refresh Blackbaud session at this time.",
                statusCode: StatusCodes.Status502BadGateway);
        }

        if (!string.IsNullOrWhiteSpace(refreshResult.RefreshToken))
        {
            user.Profile.BlackbaudRefreshToken = refreshResult.RefreshToken;
        }
        user.Profile.BlackbaudRefreshTokenUpdatedAtUtc = DateTimeOffset.UtcNow;
        user.Profile.BlackbaudAccessTokenExpiresAtUtc = refreshResult.AccessTokenExpiresAtUtc;
        user.Profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
        user.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await userRepository.UpsertAsync(user, cancellationToken);

        return Ok(new
        {
            ExpiresAtUtc = refreshResult.AccessTokenExpiresAtUtc
        });
    }

    [HttpGet("me")]
    [Authorize(Policy = "NotBanned")]
    public async Task<ActionResult<object>> Me(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        return Ok(new
        {
            user.Id,
            user.Email,
            Roles = user.Roles.Select(role => role.ToString().ToLowerInvariant())
        });
    }

    private AuthResponse ToResponse(User user)
    {
        return new AuthResponse
        {
            AccessToken = tokenService.CreateAccessToken(user),
            UserId = user.Id,
            Email = user.Email,
            Roles = user.Roles.Select(role => role.ToString().ToLowerInvariant()).ToArray()
        };
    }

    private bool IsAllowedOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return true;
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        return blackbaudOptions.Value.AllowedOrigins.Any(allowed =>
            Uri.TryCreate(allowed, UriKind.Absolute, out var allowedUri)
            && Uri.Compare(parsed, allowedUri, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0);
    }
}
