using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using OTG.Api.Authorization;
using OTG.Domain.Ideas;
using OTG.Domain.Teams;

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

    [Fact]
    public async Task IdeaOwnerOrAdminHandler_Succeeds_ForIdeaOwner()
    {
        var user = CreatePrincipal("author-1", role: "Participant");
        var idea = new Idea
        {
            Id = "idea-1",
            HackathonId = "hack-1",
            AuthorId = "author-1",
            Title = "Idea",
            Description = "Desc",
            TermsAccepted = true
        };

        var requirement = new IdeaOwnerOrAdminRequirement();
        var context = new AuthorizationHandlerContext([requirement], user, idea);

        await new IdeaOwnerOrAdminHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task IdeaOwnerOrAdminHandler_DoesNotSucceed_ForNonOwnerParticipant()
    {
        var user = CreatePrincipal("author-2", role: "Participant");
        var idea = new Idea
        {
            Id = "idea-1",
            HackathonId = "hack-1",
            AuthorId = "author-1",
            Title = "Idea",
            Description = "Desc",
            TermsAccepted = true
        };

        var requirement = new IdeaOwnerOrAdminRequirement();
        var context = new AuthorizationHandlerContext([requirement], user, idea);

        await new IdeaOwnerOrAdminHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task TeamLeaderOrAdminHandler_Succeeds_ForTeamLeader()
    {
        var user = CreatePrincipal("leader-1", role: "Participant");
        var team = new Team
        {
            Id = "team-1",
            HackathonId = "hack-1",
            Name = "Team",
            LeaderId = "leader-1"
        };

        var requirement = new TeamLeaderOrAdminRequirement();
        var context = new AuthorizationHandlerContext([requirement], user, team);

        await new TeamLeaderOrAdminHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task TeamLeaderOrAdminHandler_DoesNotSucceed_ForNonLeaderParticipant()
    {
        var user = CreatePrincipal("leader-2", role: "Participant");
        var team = new Team
        {
            Id = "team-1",
            HackathonId = "hack-1",
            Name = "Team",
            LeaderId = "leader-1"
        };

        var requirement = new TeamLeaderOrAdminRequirement();
        var context = new AuthorizationHandlerContext([requirement], user, team);

        await new TeamLeaderOrAdminHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task CommentOwnerOrAdminHandler_Succeeds_ForCommentOwner()
    {
        var user = CreatePrincipal("comment-author-1", role: "Participant");
        var comment = new Comment
        {
            Id = "comment-1",
            IdeaId = "idea-1",
            AuthorId = "comment-author-1",
            Content = "hello"
        };

        var requirement = new CommentOwnerOrAdminRequirement();
        var context = new AuthorizationHandlerContext([requirement], user, comment);

        await new CommentOwnerOrAdminHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task CommentOwnerOrAdminHandler_DoesNotSucceed_ForNonOwnerParticipant()
    {
        var user = CreatePrincipal("comment-author-2", role: "Participant");
        var comment = new Comment
        {
            Id = "comment-1",
            IdeaId = "idea-1",
            AuthorId = "comment-author-1",
            Content = "hello"
        };

        var requirement = new CommentOwnerOrAdminRequirement();
        var context = new AuthorizationHandlerContext([requirement], user, comment);

        await new CommentOwnerOrAdminHandler().HandleAsync(context);

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
