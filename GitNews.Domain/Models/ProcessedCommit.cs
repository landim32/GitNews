namespace GitNews.Domain.Models;

public class ProcessedCommit
{
    public long Id { get; set; }
    public string Repository { get; set; } = string.Empty;
    public string Sha { get; set; } = string.Empty;
    public bool IsProcessed { get; set; } = false;
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
