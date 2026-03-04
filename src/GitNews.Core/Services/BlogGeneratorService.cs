using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GitNews.Core.Interfaces;
using GitNews.Core.Models;
using Microsoft.Extensions.Options;

namespace GitNews.Core.Services;

public class BlogGeneratorService : IBlogGeneratorService
{
    private readonly HttpClient _httpClient;
    private readonly OpenAISettings _settings;

    public BlogGeneratorService(HttpClient httpClient, IOptions<OpenAISettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;

        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
    }

    public async Task<BlogPost> GenerateBlogPostAsync(RepositoryContext context)
    {
        var prompt = BuildPrompt(context);

        Console.WriteLine("Enviando prompt para o ChatGPT...");
        var response = await CallChatGptAsync(prompt);
        Console.WriteLine("Resposta recebida do ChatGPT.");

        var blogPost = ParseResponse(response);
        blogPost.Author = context.Commits.FirstOrDefault()?.Author ?? "GitNews";
        blogPost.CreatedAt = DateTime.UtcNow;
        blogPost.Slug = GenerateSlug(blogPost.Title);

        return blogPost;
    }

    private string BuildPrompt(RepositoryContext context)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Você é um redator técnico especializado em criar artigos de blog sobre projetos de software.");
        sb.AppendLine("Com base nas informações abaixo sobre um repositório GitHub, crie um artigo de blog.");
        sb.AppendLine();
        sb.AppendLine("IMPORTANTE: Responda EXATAMENTE no formato JSON abaixo, sem texto adicional:");
        sb.AppendLine("```json");
        sb.AppendLine("{");
        sb.AppendLine("  \"title\": \"Título do artigo\",");
        sb.AppendLine("  \"content\": \"Conteúdo completo do artigo em Markdown\",");
        sb.AppendLine("  \"category\": \"Categoria principal\",");
        sb.AppendLine("  \"tags\": [\"tag1\", \"tag2\", \"tag3\"]");
        sb.AppendLine("}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"## Repositório: {context.Owner}/{context.Repository}");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(context.ReadmeContent))
        {
            sb.AppendLine("### README.md:");
            sb.AppendLine(TruncateText(context.ReadmeContent, 3000));
            sb.AppendLine();
        }

        sb.AppendLine("### Commits recentes:");
        sb.AppendLine();

        foreach (var commit in context.Commits.Take(20))
        {
            sb.AppendLine($"**Commit:** {commit.Sha[..7]} - {commit.Date:yyyy-MM-dd}");
            sb.AppendLine($"**Autor:** {commit.Author}");
            sb.AppendLine($"**Mensagem:** {commit.Message}");
            sb.AppendLine();

            if (commit.Files.Count > 0)
            {
                sb.AppendLine("**Arquivos alterados:**");
                foreach (var file in commit.Files.Take(10))
                {
                    sb.AppendLine($"- `{file.FileName}` ({file.Status}: +{file.Additions} -{file.Deletions})");

                    if (!string.IsNullOrEmpty(file.Patch))
                    {
                        sb.AppendLine("```diff");
                        sb.AppendLine(file.Patch);
                        sb.AppendLine("```");
                    }
                }
                sb.AppendLine();
            }
        }

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("Crie um artigo de blog completo, informativo e bem estruturado sobre este projeto.");
        sb.AppendLine("O artigo deve explicar o que o projeto faz, as mudanças recentes e seu impacto.");
        sb.AppendLine("Escreva em português brasileiro.");
        sb.AppendLine("Retorne APENAS o JSON, sem markdown code fences ao redor.");

        return sb.ToString();
    }

    private async Task<string> CallChatGptAsync(string prompt)
    {
        var requestBody = new
        {
            model = _settings.Model,
            messages = new[]
            {
                new { role = "system", content = "Você é um redator técnico que cria artigos de blog sobre projetos de software. Responda sempre em JSON válido." },
                new { role = "user", content = prompt }
            },
            temperature = 0.7,
            max_tokens = 4000
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("/v1/chat/completions", content);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        var chatResponse = JsonSerializer.Deserialize<ChatCompletionResponse>(responseJson);

        return chatResponse?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
    }

    private static BlogPost ParseResponse(string response)
    {
        // Remove possíveis code fences do JSON
        var cleanJson = response
            .Replace("```json", "")
            .Replace("```", "")
            .Trim();

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var parsed = JsonSerializer.Deserialize<BlogPostResponse>(cleanJson, options);

            return new BlogPost
            {
                Title = parsed?.Title ?? "Sem título",
                Content = parsed?.Content ?? "Sem conteúdo",
                Category = parsed?.Category ?? "Tecnologia",
                Tags = parsed?.Tags ?? new List<string> { "desenvolvimento", "github" }
            };
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Erro ao fazer parse do JSON da resposta: {ex.Message}");
            Console.WriteLine("Usando resposta bruta como conteúdo.");

            return new BlogPost
            {
                Title = "Atualização do Projeto",
                Content = response,
                Category = "Tecnologia",
                Tags = new List<string> { "desenvolvimento", "github" }
            };
        }
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

        // Remove caracteres que não sejam letras, números ou hífens
        var cleanSlug = new string(slug.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());

        // Remove hífens duplicados
        while (cleanSlug.Contains("--"))
            cleanSlug = cleanSlug.Replace("--", "-");

        return cleanSlug.Trim('-');
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        return text[..maxLength] + "\n\n... (conteúdo truncado)";
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

internal class BlogPostResponse
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }
}

#endregion
