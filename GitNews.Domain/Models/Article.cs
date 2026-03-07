namespace GitNews.Domain.Models;

public class Article
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public float[]? Embedding { get; set; }
    public string? ImageBase64 { get; set; }
    public bool IsProcessed { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
