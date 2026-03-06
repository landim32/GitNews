namespace GitNews.DTO;

public class RepositoryContextInfo
{
    public string Owner { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public string ReadmeContent { get; set; } = string.Empty;
    public List<CommitInfoDto> Commits { get; set; } = new();
    public int TotalCommitCount { get; set; }
}
