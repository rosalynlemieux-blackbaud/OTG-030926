using Microsoft.AspNetCore.Authorization;
using OTG.Api.Extensions;
using OTG.Application.Abstractions.Repositories;

namespace OTG.Api.Authorization;

public sealed class NotBannedRequirement : IAuthorizationRequirement
{
}

public sealed class NotBannedHandler(IUserRepository userRepository) : AuthorizationHandler<NotBannedRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, NotBannedRequirement requirement)
    {
        var userId = context.User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        var user = await userRepository.GetByIdAsync(userId);
        if (user?.Profile?.Banned is false)
        {
            context.Succeed(requirement);
        }
    }
}
