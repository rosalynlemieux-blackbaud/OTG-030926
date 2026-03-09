namespace OTG.Domain.Hackathons;

public sealed class Milestone
{
    public required string Id { get; init; }
    public required string Title { get; set; }
    public DateTimeOffset Date { get; set; }
    public string? Description { get; set; }
    public bool IsCompleted { get; set; }
    public int SortOrder { get; set; }
}
