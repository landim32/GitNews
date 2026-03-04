namespace GitNews.Core.Models;

public class RepositoryContext
{
    public string Owner { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public string ReadmeContent { get; set; } = string.Empty;
    public List<CommitInfo> Commits { get; set; } = new();
}
