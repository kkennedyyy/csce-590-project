namespace ClassFinder.Api.Models;

public class ProcessedFeed
{
    public int Id { get; set; }
    public string FeedName { get; set; } = string.Empty;   // blob/file name
    public string FeedType { get; set; } = string.Empty;   // users, classes, enrollments
    public DateTimeOffset ProcessedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? ETag { get; set; }
}