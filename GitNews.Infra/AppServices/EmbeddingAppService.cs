using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GitNews.DTO;
using GitNews.Infra.Interfaces.AppServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GitNews.Infra.AppServices;

public class EmbeddingAppService : IEmbeddingAppService
{
    private readonly HttpClient _httpClient;
    private readonly OpenAISettings _settings;
    private readonly ILogger<EmbeddingAppService> _logger;

    public EmbeddingAppService(HttpClient httpClient, IOptions<OpenAISettings> settings, ILogger<EmbeddingAppService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        var baseUrl = _settings.BaseUrl.TrimEnd('/') + "/";
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        var truncated = text.Length > 30000 ? text[..30000] : text;

        var requestBody = new
        {
            model = "text-embedding-3-small",
            input = truncated
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("embeddings", content);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        var embeddingResponse = JsonSerializer.Deserialize<EmbeddingResponse>(responseJson);

        return embeddingResponse?.Data?.FirstOrDefault()?.Embedding
            ?? throw new InvalidOperationException("Failed to generate embedding.");
    }
}

#region Embedding Response Models

internal class EmbeddingResponse
{
    [JsonPropertyName("data")]
    public List<EmbeddingData>? Data { get; set; }
}

internal class EmbeddingData
{
    [JsonPropertyName("embedding")]
    public float[]? Embedding { get; set; }
}

#endregion
