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
        var sb = new StringBuilder();

        sb.AppendLine("Você é um redator técnico especializado em criar artigos de blog sobre desenvolvimento de software.");
        sb.AppendLine();
        sb.AppendLine("## SUA TAREFA");
        sb.AppendLine();
        sb.AppendLine("Analise os commits abaixo (incluindo os diffs de código) e identifique **novidades técnicas** implementadas, como:");
        sb.AppendLine("- Novos pacotes/bibliotecas adicionados (NuGet, npm, pip, etc.)");
        sb.AppendLine("- Design patterns aplicados (Repository, Factory, Strategy, etc.)");
        sb.AppendLine("- Padrões arquiteturais (Clean Architecture, CQRS, Event Sourcing, etc.)");
        sb.AppendLine("- Novas tecnologias ou frameworks integrados");
        sb.AppendLine("- Técnicas de refatoração relevantes");
        sb.AppendLine("- Configurações de infraestrutura (Docker, CI/CD, etc.)");
        sb.AppendLine();
        sb.AppendLine("## REGRAS PARA GERAÇÃO DO ARTIGO");
        sb.AppendLine();
        sb.AppendLine("1. **Cada novidade técnica identificada deve virar um tópico do artigo.**");
        sb.AppendLine("2. **Para cada tópico, escreva exemplos de código originais inspirados no projeto** (use blocos de código Markdown).");
        sb.AppendLine("   - Os exemplos devem ser trechos de código limpos, legíveis e que compilam/funcionam sozinhos.");
        sb.AppendLine("   - NUNCA copie o código da seção de commits diretamente. Reescreva os trechos como exemplos didáticos.");
        sb.AppendLine("   - NUNCA use formato diff, patches, ou linhas começando com +/-.");
        sb.AppendLine("   - NUNCA inclua metadados de commit (sha, autor, data) no artigo.");
        sb.AppendLine("3. **Explique o que cada trecho faz e por que aquela abordagem foi escolhida.**");
        sb.AppendLine("4. **O título NÃO deve conter o nome do projeto/repositório.** Foque na técnica ou novidade (ex: 'Implementando Repository Pattern com EF Core').");
        sb.AppendLine("5. **O artigo deve ter entre 800 e 1200 palavras.** Seja detalhado e completo.");
        sb.AppendLine();

        if (isNewProject)
        {
            sb.AppendLine("6. **Este é um projeto NOVO (primeiros commits).** Inclua uma seção inicial explicando o que o projeto faz, sua utilidade e propósito.");
        }
        else
        {
            sb.AppendLine("6. **NÃO faça um artigo sobre o que é o projeto.** Foque exclusivamente nas novidades técnicas encontradas nos commits recentes.");
            sb.AppendLine("   O nome do projeto pode aparecer no conteúdo como contexto, mas o artigo deve ser sobre as técnicas/pacotes/patterns.");
        }

        sb.AppendLine();
        sb.AppendLine("7. Se não encontrar nenhuma novidade técnica relevante nos commits, retorne um artigo com título \"Sem novidades\" e conteúdo vazio.");
        sb.AppendLine();
        sb.AppendLine("## FORMATO DE RESPOSTA");
        sb.AppendLine();
        sb.AppendLine("Responda EXATAMENTE neste formato JSON, sem texto adicional:");
        sb.AppendLine("{");
        sb.AppendLine("  \"title\": \"Título do artigo focado na novidade técnica principal\",");
        sb.AppendLine("  \"content\": \"Conteúdo completo do artigo em Markdown, com trechos de código como exemplo\",");
        sb.AppendLine("  \"category\": \"Categoria principal (ex: Arquitetura, Pacotes, Patterns, DevOps)\",");
        sb.AppendLine("  \"tags\": [\"tag1\", \"tag2\", \"tag3\"]");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("---");
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
        var cleanJson = response
            .Replace("```json", "")
            .Replace("```", "")
            .Trim();

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            };

            cleanJson = FixJsonNewlines(cleanJson);

            var parsed = JsonSerializer.Deserialize<BlogPostResponse>(cleanJson, options);

            return new BlogPostInfo
            {
                Title = parsed?.Title ?? "Sem título",
                Content = parsed?.Content ?? "Sem conteúdo",
                Category = parsed?.Category ?? "Tecnologia",
                Tags = parsed?.Tags ?? new List<string> { "desenvolvimento", "github" }
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse JSON response. Using raw response as content");

            return new BlogPostInfo
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

        var cleanSlug = new string(slug.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());

        while (cleanSlug.Contains("--"))
            cleanSlug = cleanSlug.Replace("--", "-");

        return cleanSlug.Trim('-');
    }

    private static string FixJsonNewlines(string json)
    {
        var result = new StringBuilder(json.Length);
        bool insideString = false;
        bool escaped = false;

        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];

            if (escaped)
            {
                result.Append(c);
                escaped = false;
                continue;
            }

            if (c == '\\' && insideString)
            {
                result.Append(c);
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                insideString = !insideString;
                result.Append(c);
                continue;
            }

            if (insideString && (c == '\n' || c == '\r'))
            {
                if (c == '\r' && i + 1 < json.Length && json[i + 1] == '\n')
                    i++;
                result.Append("\\n");
                continue;
            }

            result.Append(c);
        }

        return result.ToString();
    }

    private static string ExtractAddedCode(string patch)
    {
        var lines = patch.Split('\n')
            .Where(line => line.StartsWith('+') && !line.StartsWith("+++"))
            .Select(line => line[1..]);

        return string.Join('\n', lines).Trim();
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
