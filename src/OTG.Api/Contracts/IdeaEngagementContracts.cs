namespace OTG.Api.Contracts;

public sealed class UpsertCommentRequest
{
    public required string Content { get; init; }
    public string? ParentId { get; init; }
}

public sealed class UpdateCommentRequest
{
    public required string Content { get; init; }
}
