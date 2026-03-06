namespace GitNews.DTO;

public class ProcessedCommitInfo
{
    public long Id { get; set; }
    public string Repository { get; set; } = string.Empty;
    public string Sha { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
}
