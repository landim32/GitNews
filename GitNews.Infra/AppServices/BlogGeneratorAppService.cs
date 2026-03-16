using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GitNews.DTO;
using GitNews.Infra.Interfaces.AppServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GitNews.Infra.AppServices;

public class BlogGeneratorAppService : IBlogGeneratorAppService
{
    private readonly HttpClient _httpClient;
    private readonly OpenAISettings _settings;
    private readonly ILogger<BlogGeneratorAppService> _logger;

    public BlogGeneratorAppService(HttpClient httpClient, IOptions<OpenAISettings> settings, ILogger<BlogGeneratorAppService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        var baseUrl = _settings.BaseUrl.TrimEnd('/') + "/";
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
    }

    public async Task<BlogPostInfo> GenerateBlogPostAsync(RepositoryContextInfo context)
    {
        var prompt = BuildPrompt(context);

        _logger.LogInformation("Sending prompt to ChatGPT...");
        var response = await CallChatGptAsync(prompt);
        _logger.LogInformation("Response received from ChatGPT");

        var blogPost = ParseResponse(response);
        blogPost.Author = context.Commits.FirstOrDefault()?.Author ?? "GitNews";
        blogPost.CreatedAt = DateTime.UtcNow;
        blogPost.Slug = GenerateSlug(blogPost.Title);

        return blogPost;
    }

    private string BuildPrompt(RepositoryContextInfo context)
    {
        var isNewProject = context.TotalCommitCount <= 3;

        var promptTemplate = LoadPromptTemplate();
        var repoUrl = $"https://github.com/{context.Owner}/{context.Repository}";
        var newProjectRule = isNewProject
            ? "7. **Este é um projeto NOVO (primeiros commits).** Mesmo assim, foque nas técnicas utilizadas. Cite o projeto apenas uma vez no final."
            : "7. **NÃO faça um artigo sobre o que é o projeto.** Foque exclusivamente nas novidades técnicas encontradas nos commits recentes.";

        var sb = new StringBuilder(promptTemplate
            .Replace("{{REPO_URL}}", repoUrl)
            .Replace("{{NEW_PROJECT_RULE}}", newProjectRule));

        sb.AppendLine();
        sb.AppendLine($"## Repositório: {context.Owner}/{context.Repository}");
        sb.AppendLine($"## Total de commits no repositório: {context.TotalCommitCount}");
        sb.AppendLine();

        if (isNewProject && !string.IsNullOrEmpty(context.ReadmeContent))
        {
            sb.AppendLine("### README.md:");
            sb.AppendLine(TruncateText(context.ReadmeContent, 3000));
            sb.AppendLine();
        }

        sb.AppendLine("### Commits recentes (analise os diffs para identificar novidades):");
        sb.AppendLine();

        const int maxPromptLength = 12000;

        foreach (var commit in context.Commits.Take(10))
        {
            if (sb.Length > maxPromptLength) break;

            sb.AppendLine($"**Commit:** {commit.Sha[..7]} - {commit.Date:yyyy-MM-dd}");
            sb.AppendLine($"**Autor:** {commit.Author}");
            sb.AppendLine($"**Mensagem:** {commit.Message}");
            sb.AppendLine();

            if (commit.Files.Count > 0)
            {
                sb.AppendLine("**Arquivos alterados:**");
                foreach (var file in commit.Files.Take(5))
                {
                    sb.AppendLine($"- `{file.FileName}` ({file.Status}: +{file.Additions} -{file.Deletions})");

                    if (!string.IsNullOrEmpty(file.Patch) && sb.Length < maxPromptLength)
                    {
                        var addedCode = ExtractAddedCode(file.Patch);
                        if (!string.IsNullOrWhiteSpace(addedCode))
                        {
                            var extension = Path.GetExtension(file.FileName).TrimStart('.');
                            sb.AppendLine($"```{extension}");
                            sb.AppendLine(TruncateText(addedCode, 500));
                            sb.AppendLine("```");
                        }
                    }
                }
                sb.AppendLine();
            }
        }

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("Escreva em português brasileiro.");
        sb.AppendLine("Retorne APENAS no formato especificado acima (TITLE/CATEGORY/TAGS + conteúdo Markdown).");

        return sb.ToString();
    }

    private async Task<string> CallChatGptAsync(string prompt)
    {
        var requestBody = new
        {
            model = _settings.Model,
            messages = new[]
            {
                new { role = "system", content = "Você é um redator técnico que cria artigos de blog sobre projetos de software. Responda sempre em Markdown puro, nunca em JSON." },
                new { role = "user", content = prompt }
            },
            temperature = 0.7,
            max_tokens = 4000
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        const int maxRetries = 3;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            var response = await _httpClient.PostAsync("chat/completions", content);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                var delay = (int)Math.Pow(2, attempt + 1) * 10;
                _logger.LogWarning("Rate limit hit. Waiting {Delay}s before retrying ({Attempt}/{MaxRetries})...", delay, attempt + 1, maxRetries);
                await Task.Delay(TimeSpan.FromSeconds(delay));
                continue;
            }

            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var chatResponse = JsonSerializer.Deserialize<ChatCompletionResponse>(responseJson);

            return chatResponse?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
        }

        throw new HttpRequestException("Rate limit exceeded after all retries.");
    }

    private BlogPostInfo ParseResponse(string response)
    {
        var text = response.Trim();
        var lines = text.Split('\n');

        string title = "Sem título";
        string category = "Tecnologia";
        var tags = new List<string> { "desenvolvimento", "github" };
        int contentStartLine = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (line.StartsWith("TITLE:", StringComparison.OrdinalIgnoreCase))
            {
                title = line["TITLE:".Length..].Trim();
                contentStartLine = i + 1;
            }
            else if (line.StartsWith("CATEGORY:", StringComparison.OrdinalIgnoreCase))
            {
                category = line["CATEGORY:".Length..].Trim();
                contentStartLine = i + 1;
            }
            else if (line.StartsWith("TAGS:", StringComparison.OrdinalIgnoreCase))
            {
                var tagsStr = line["TAGS:".Length..].Trim();
                tags = tagsStr.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
                contentStartLine = i + 1;
            }
            else if (!string.IsNullOrWhiteSpace(line))
            {
                // First non-metadata, non-empty line — content starts here
                contentStartLine = i;
                break;
            }
        }

        var content = string.Join('\n', lines.Skip(contentStartLine)).Trim();

        return new BlogPostInfo
        {
            Title = title,
            Content = content,
            Category = category,
            Tags = tags
        };
    }

    private static string GenerateSlug(string title)
    {
        var slug = title.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("ã", "a").Replace("á", "a").Replace("â", "a").Replace("à", "a")
            .Replace("é", "e").Replace("ê", "e")
            .Replace("í", "i")
            .Replace("ó", "o").Replace("ô", "o").Replace("õ", "o")
            .Replace("ú", "u").Replace("ü", "u")
            .Replace("ç", "c");

        var cleanSlug = new string(slug.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());

        while (cleanSlug.Contains("--"))
            cleanSlug = cleanSlug.Replace("--", "-");

        return cleanSlug.Trim('-');
    }

    private static string ExtractAddedCode(string patch)
    {
        var lines = patch.Split('\n')
            .Where(line => line.StartsWith('+') && !line.StartsWith("+++"))
            .Select(line => line[1..]);

        return string.Join('\n', lines).Trim();
    }

    private static string LoadPromptTemplate()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "blog-prompt.md"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "blog-prompt.md"),
            Path.Combine(Directory.GetCurrentDirectory(), "blog-prompt.md"),
        };

        foreach (var path in candidates)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
                return File.ReadAllText(fullPath);
        }

        throw new FileNotFoundException("blog-prompt.md not found. Ensure it exists in the project root.");
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        return text[..maxLength] + "\n\n... (truncated)";
    }
}

#region ChatGPT Response Models

internal class ChatCompletionResponse
{
    [JsonPropertyName("choices")]
    public List<ChatChoice>? Choices { get; set; }
}

internal class ChatChoice
{
    [JsonPropertyName("message")]
    public ChatMessage? Message { get; set; }
}

internal class ChatMessage
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

#endregion
