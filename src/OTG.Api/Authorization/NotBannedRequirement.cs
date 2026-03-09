using Microsoft.AspNetCore.Authorization;
using OTG.Api.Extensions;
using OTG.Application.Abstractions.Repositories;
using OTG.Domain.Ideas;
using OTG.Domain.Teams;

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

public sealed class IdeaOwnerOrAdminRequirement : IAuthorizationRequirement
{
}

public sealed class IdeaOwnerOrAdminHandler : AuthorizationHandler<IdeaOwnerOrAdminRequirement, Idea>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, IdeaOwnerOrAdminRequirement requirement, Idea resource)
    {
        var userId = context.User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Task.CompletedTask;
        }

        if (context.User.IsInRole("Admin") || string.Equals(resource.AuthorId, userId, StringComparison.Ordinal))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public sealed class TeamLeaderOrAdminRequirement : IAuthorizationRequirement
{
}

public sealed class TeamLeaderOrAdminHandler : AuthorizationHandler<TeamLeaderOrAdminRequirement, Team>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TeamLeaderOrAdminRequirement requirement, Team resource)
    {
        var userId = context.User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Task.CompletedTask;
        }

        if (context.User.IsInRole("Admin") || string.Equals(resource.LeaderId, userId, StringComparison.Ordinal))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
