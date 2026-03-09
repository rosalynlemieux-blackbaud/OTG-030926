namespace OTG.Domain.Hackathons;

public sealed class JudgingCriterion
{
    public required string Id { get; init; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public List<string> BulletPoints { get; set; } = [];
    public string? Icon { get; set; }
    public int MaxScore { get; set; }
    public decimal Weight { get; set; }
    public int SortOrder { get; set; }
}
