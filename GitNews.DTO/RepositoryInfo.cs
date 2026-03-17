namespace GitNews.DTO;

public class RepositoryInfo
{
    public string Owner { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FullName => $"{Owner}/{Name}";
}
