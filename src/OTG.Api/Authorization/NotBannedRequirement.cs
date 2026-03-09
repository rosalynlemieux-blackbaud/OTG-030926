using Microsoft.AspNetCore.Authorization;
using OTG.Api.Extensions;
using OTG.Application.Abstractions.Repositories;
using OTG.Domain.Ideas;

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

public sealed class AssignedJudgeOrAdminRequirement : IAuthorizationRequirement
{
}

public sealed class AssignedJudgeOrAdminHandler : AuthorizationHandler<AssignedJudgeOrAdminRequirement, Idea>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AssignedJudgeOrAdminRequirement requirement, Idea resource)
    {
        var userId = context.User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Task.CompletedTask;
        }

        if (context.User.IsInRole("Admin") || resource.AssignedJudgeIds.Contains(userId, StringComparer.Ordinal))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
