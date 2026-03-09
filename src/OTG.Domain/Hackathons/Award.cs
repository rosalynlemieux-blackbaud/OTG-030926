namespace OTG.Domain.Hackathons;

public sealed class Award
{
    public required string Id { get; init; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public string? Prize { get; set; }
    public string? BackgroundImageUrl { get; set; }
    public string? TrackId { get; set; }
    public int SortOrder { get; set; }
}
