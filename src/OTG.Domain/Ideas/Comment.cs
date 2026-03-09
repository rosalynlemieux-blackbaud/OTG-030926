using OTG.Domain.Common;

namespace OTG.Domain.Ideas;

public sealed class Comment : AuditableEntity
{
    public required string IdeaId { get; init; }
    public required string AuthorId { get; init; }
    public required string Content { get; set; }
    public string? ParentId { get; set; }
    public bool IsModerated { get; set; }
    public string? ModerationReason { get; set; }
    public string? ModeratedBy { get; set; }
    public DateTimeOffset? ModeratedAtUtc { get; set; }
}
