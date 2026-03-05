using System.Text;
using GitNews.Domain.Interfaces;
using GitNews.Domain.Models;
using Microsoft.Extensions.Options;

namespace GitNews.Infra.Services;

public class MarkdownWriter : IMarkdownWriter
{
    private readonly OutputSettings _settings;

    public MarkdownWriter(IOptions<OutputSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task<string> WritePostAsync(BlogPost post)
    {
        var directory = _settings.OutputDirectory;

        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            Console.WriteLine($"Diretório criado: {directory}");
        }

        var fileName = $"{post.CreatedAt:yyyy-MM-dd}-{post.Slug}.md";
        var filePath = Path.Combine(directory, fileName);

        var content = BuildMarkdownContent(post);

        await File.WriteAllTextAsync(filePath, content, Encoding.UTF8);

        Console.WriteLine($"Artigo salvo em: {filePath}");
        return filePath;
    }

    private static string BuildMarkdownContent(BlogPost post)
    {
        var sb = new StringBuilder();

        // Front matter (YAML)
        sb.AppendLine("---");
        sb.AppendLine($"title: \"{EscapeYaml(post.Title)}\"");
        sb.AppendLine($"date: {post.CreatedAt:yyyy-MM-ddTHH:mm:ssZ}");
        sb.AppendLine($"author: \"{EscapeYaml(post.Author)}\"");
        sb.AppendLine($"category: \"{EscapeYaml(post.Category)}\"");
        sb.AppendLine("tags:");
        foreach (var tag in post.Tags)
        {
            sb.AppendLine($"  - \"{EscapeYaml(tag)}\"");
        }
        sb.AppendLine($"slug: \"{post.Slug}\"");
        sb.AppendLine("---");
        sb.AppendLine();

        // Conteúdo do artigo
        sb.AppendLine(post.Content);

        return sb.ToString();
    }

    private static string EscapeYaml(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }
}
