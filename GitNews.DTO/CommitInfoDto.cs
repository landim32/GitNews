namespace GitNews.DTO;

public class CommitInfoDto
{
    public string Sha { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public DateTimeOffset Date { get; set; }
    public List<FileChangeInfo> Files { get; set; } = new();
}

public class FileChangeInfo
{
    public string FileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Additions { get; set; }
    public int Deletions { get; set; }
    public string Patch { get; set; } = string.Empty;
}
