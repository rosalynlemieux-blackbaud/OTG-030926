using OTG.Domain.Hackathons;
using OTG.Domain.Identity;
using OTG.Domain.Ideas;
using OTG.Domain.Teams;
using OTG.Infrastructure.Cosmos.Documents;

namespace OTG.Infrastructure.Cosmos.Mapping;

internal static class DocumentMappers
{
    public static UserDocument ToDocument(this User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        PasswordHash = user.PasswordHash,
        EmailConfirmed = user.EmailConfirmed,
        Roles = user.Roles,
        Profile = user.Profile,
        CreatedAtUtc = user.CreatedAtUtc,
        UpdatedAtUtc = user.UpdatedAtUtc
    };

    public static User ToDomain(this UserDocument document) => new()
    {
        Id = document.Id,
        Email = document.Email,
        PasswordHash = document.PasswordHash,
        EmailConfirmed = document.EmailConfirmed,
        Roles = document.Roles,
        Profile = document.Profile,
        CreatedAtUtc = document.CreatedAtUtc,
        UpdatedAtUtc = document.UpdatedAtUtc
    };

    public static HackathonDocument ToDocument(this Hackathon hackathon) => new()
    {
        Id = hackathon.HackathonId,
        HackathonId = hackathon.HackathonId,
        Name = hackathon.Name,
        Description = hackathon.Description,
        LogoUrl = hackathon.LogoUrl,
        LedeImageUrl = hackathon.LedeImageUrl,
        RegistrationOpen = hackathon.RegistrationOpen,
        SubmissionDeadline = hackathon.SubmissionDeadline,
        JudgingStart = hackathon.JudgingStart,
        JudgingEnd = hackathon.JudgingEnd,
        RulesMarkdown = hackathon.RulesMarkdown,
        Faq = hackathon.Faq,
        Terms = hackathon.Terms,
        SwagHtml = hackathon.SwagHtml,
        Tracks = hackathon.Tracks,
        Awards = hackathon.Awards,
        JudgingCriteria = hackathon.JudgingCriteria,
        Milestones = hackathon.Milestones,
        CreatedAtUtc = hackathon.CreatedAtUtc,
        UpdatedAtUtc = hackathon.UpdatedAtUtc
    };

    public static Hackathon ToDomain(this HackathonDocument document) => new()
    {
        Id = document.HackathonId,
        HackathonId = document.HackathonId,
        Name = document.Name,
        Description = document.Description,
        LogoUrl = document.LogoUrl,
        LedeImageUrl = document.LedeImageUrl,
        RegistrationOpen = document.RegistrationOpen,
        SubmissionDeadline = document.SubmissionDeadline,
        JudgingStart = document.JudgingStart,
        JudgingEnd = document.JudgingEnd,
        RulesMarkdown = document.RulesMarkdown,
        Faq = document.Faq,
        Terms = document.Terms,
        SwagHtml = document.SwagHtml,
        Tracks = document.Tracks,
        Awards = document.Awards,
        JudgingCriteria = document.JudgingCriteria,
        Milestones = document.Milestones,
        CreatedAtUtc = document.CreatedAtUtc,
        UpdatedAtUtc = document.UpdatedAtUtc
    };

    public static IdeaDocument ToDocument(this Idea idea) => new()
    {
        Id = idea.Id,
        HackathonId = idea.HackathonId,
        Title = idea.Title,
        Description = idea.Description,
        Status = idea.Status,
        AuthorId = idea.AuthorId,
        TeamId = idea.TeamId,
        TrackId = idea.TrackId,
        Tags = idea.Tags,
        Attachments = idea.Attachments,
        VideoUrl = idea.VideoUrl,
        RepoUrl = idea.RepoUrl,
        DemoUrl = idea.DemoUrl,
        Votes = idea.Votes,
        SubmittedAtUtc = idea.SubmittedAtUtc,
        AssignedJudgeIds = idea.AssignedJudgeIds,
        AwardIds = idea.AwardIds,
        TermsAccepted = idea.TermsAccepted,
        CreatedAtUtc = idea.CreatedAtUtc,
        UpdatedAtUtc = idea.UpdatedAtUtc
    };

    public static Idea ToDomain(this IdeaDocument document) => new()
    {
        Id = document.Id,
        HackathonId = document.HackathonId,
        Title = document.Title,
        Description = document.Description,
        Status = document.Status,
        AuthorId = document.AuthorId,
        TeamId = document.TeamId,
        TrackId = document.TrackId,
        Tags = document.Tags,
        Attachments = document.Attachments,
        VideoUrl = document.VideoUrl,
        RepoUrl = document.RepoUrl,
        DemoUrl = document.DemoUrl,
        Votes = document.Votes,
        SubmittedAtUtc = document.SubmittedAtUtc,
        AssignedJudgeIds = document.AssignedJudgeIds,
        AwardIds = document.AwardIds,
        TermsAccepted = document.TermsAccepted,
        CreatedAtUtc = document.CreatedAtUtc,
        UpdatedAtUtc = document.UpdatedAtUtc
    };

    public static CommentDocument ToDocument(this Comment comment) => new()
    {
        Id = comment.Id,
        IdeaId = comment.IdeaId,
        AuthorId = comment.AuthorId,
        Content = comment.Content,
        ParentId = comment.ParentId,
        IsModerated = comment.IsModerated,
        ModerationReason = comment.ModerationReason,
        ModeratedBy = comment.ModeratedBy,
        ModeratedAtUtc = comment.ModeratedAtUtc,
        CreatedAtUtc = comment.CreatedAtUtc,
        UpdatedAtUtc = comment.UpdatedAtUtc
    };

    public static Comment ToDomain(this CommentDocument document) => new()
    {
        Id = document.Id,
        IdeaId = document.IdeaId,
        AuthorId = document.AuthorId,
        Content = document.Content,
        ParentId = document.ParentId,
        IsModerated = document.IsModerated,
        ModerationReason = document.ModerationReason,
        ModeratedBy = document.ModeratedBy,
        ModeratedAtUtc = document.ModeratedAtUtc,
        CreatedAtUtc = document.CreatedAtUtc,
        UpdatedAtUtc = document.UpdatedAtUtc
    };

    public static RatingDocument ToDocument(this IdeaRating rating) => new()
    {
        Id = rating.Id,
        IdeaId = rating.IdeaId,
        JudgeId = rating.JudgeId,
        Scores = rating.Scores,
        OverallFeedback = rating.OverallFeedback,
        WeightedScore = rating.WeightedScore,
        IsModerated = rating.IsModerated,
        ModerationReason = rating.ModerationReason,
        ModeratedBy = rating.ModeratedBy,
        ModeratedAtUtc = rating.ModeratedAtUtc,
        CreatedAtUtc = rating.CreatedAtUtc,
        UpdatedAtUtc = rating.UpdatedAtUtc
    };

    public static IdeaRating ToDomain(this RatingDocument document) => new()
    {
        Id = document.Id,
        IdeaId = document.IdeaId,
        JudgeId = document.JudgeId,
        Scores = document.Scores,
        OverallFeedback = document.OverallFeedback,
        WeightedScore = document.WeightedScore,
        IsModerated = document.IsModerated,
        ModerationReason = document.ModerationReason,
        ModeratedBy = document.ModeratedBy,
        ModeratedAtUtc = document.ModeratedAtUtc,
        CreatedAtUtc = document.CreatedAtUtc,
        UpdatedAtUtc = document.UpdatedAtUtc
    };

    public static TeamDocument ToDocument(this Team team) => new()
    {
        Id = team.Id,
        HackathonId = team.HackathonId,
        Name = team.Name,
        Description = team.Description,
        ImageUrl = team.ImageUrl,
        LeaderId = team.LeaderId,
        MaxSize = team.MaxSize,
        Skills = team.Skills,
        Members = team.Members,
        CreatedAtUtc = team.CreatedAtUtc,
        UpdatedAtUtc = team.UpdatedAtUtc
    };

    public static Team ToDomain(this TeamDocument document) => new()
    {
        Id = document.Id,
        HackathonId = document.HackathonId,
        Name = document.Name,
        Description = document.Description,
        ImageUrl = document.ImageUrl,
        LeaderId = document.LeaderId,
        MaxSize = document.MaxSize,
        Skills = document.Skills,
        Members = document.Members,
        CreatedAtUtc = document.CreatedAtUtc,
        UpdatedAtUtc = document.UpdatedAtUtc
    };

    public static TeamJoinRequestDocument ToDocument(this TeamJoinRequest request) => new()
    {
        Id = request.Id,
        TeamId = request.TeamId,
        UserId = request.UserId,
        Message = request.Message,
        Status = request.Status,
        CreatedAtUtc = request.CreatedAtUtc,
        UpdatedAtUtc = request.UpdatedAtUtc
    };

    public static TeamJoinRequest ToDomain(this TeamJoinRequestDocument document) => new()
    {
        Id = document.Id,
        TeamId = document.TeamId,
        UserId = document.UserId,
        Message = document.Message,
        Status = document.Status,
        CreatedAtUtc = document.CreatedAtUtc,
        UpdatedAtUtc = document.UpdatedAtUtc
    };

    public static TeamInviteDocument ToDocument(this TeamInvite invite) => new()
    {
        Id = invite.Id,
        TeamId = invite.TeamId,
        Email = invite.Email,
        InvitedBy = invite.InvitedBy,
        Token = invite.Token,
        Status = invite.Status,
        NeedsApproval = invite.NeedsApproval,
        ApprovedBy = invite.ApprovedBy,
        ExpiresAtUtc = invite.ExpiresAtUtc,
        CreatedAtUtc = invite.CreatedAtUtc,
        UpdatedAtUtc = invite.UpdatedAtUtc
    };

    public static TeamInvite ToDomain(this TeamInviteDocument document) => new()
    {
        Id = document.Id,
        TeamId = document.TeamId,
        Email = document.Email,
        InvitedBy = document.InvitedBy,
        Token = document.Token,
        Status = document.Status,
        NeedsApproval = document.NeedsApproval,
        ApprovedBy = document.ApprovedBy,
        ExpiresAtUtc = document.ExpiresAtUtc,
        CreatedAtUtc = document.CreatedAtUtc,
        UpdatedAtUtc = document.UpdatedAtUtc
    };
}
