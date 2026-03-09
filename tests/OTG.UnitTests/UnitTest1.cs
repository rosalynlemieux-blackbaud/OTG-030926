using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using OTG.Api.Authorization;
using OTG.Domain.Ideas;

namespace OTG.UnitTests;

public sealed class AuthorizationHandlerTests
{
    [Fact]
    public async Task AssignedJudgeOrAdminHandler_Succeeds_ForAssignedJudge()
    {
        var user = CreatePrincipal("judge-1", role: "Judge");
        var idea = new Idea
        {
            Id = "idea-1",
            HackathonId = "hack-1",
            AuthorId = "author-1",
            Title = "Idea",
            Description = "Desc",
            AssignedJudgeIds = ["judge-1"],
            TermsAccepted = true
        };

        var requirement = new AssignedJudgeOrAdminRequirement();
        var context = new AuthorizationHandlerContext([requirement], user, idea);

        await new AssignedJudgeOrAdminHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task AssignedJudgeOrAdminHandler_Succeeds_ForAdmin()
    {
        var user = CreatePrincipal("admin-1", role: "Admin");
        var idea = new Idea
        {
            Id = "idea-1",
            HackathonId = "hack-1",
            AuthorId = "author-1",
            Title = "Idea",
            Description = "Desc",
            AssignedJudgeIds = [],
            TermsAccepted = true
        };

        var requirement = new AssignedJudgeOrAdminRequirement();
        var context = new AuthorizationHandlerContext([requirement], user, idea);

        await new AssignedJudgeOrAdminHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task AssignedJudgeOrAdminHandler_DoesNotSucceed_ForUnassignedJudge()
    {
        var user = CreatePrincipal("judge-2", role: "Judge");
        var idea = new Idea
        {
            Id = "idea-1",
            HackathonId = "hack-1",
            AuthorId = "author-1",
            Title = "Idea",
            Description = "Desc",
            AssignedJudgeIds = ["judge-1"],
            TermsAccepted = true
        };

        var requirement = new AssignedJudgeOrAdminRequirement();
        var context = new AuthorizationHandlerContext([requirement], user, idea);

        await new AssignedJudgeOrAdminHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    private static ClaimsPrincipal CreatePrincipal(string userId, string role)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, role)
        ], "test");
        return new ClaimsPrincipal(identity);
    }
}
