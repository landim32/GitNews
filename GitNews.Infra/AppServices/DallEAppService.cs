using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GitNews.DTO;
using GitNews.Infra.Interfaces.AppServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GitNews.Infra.AppServices;

public class DallEAppService : IDallEAppService
{
    private readonly HttpClient _httpClient;
    private readonly OpenAISettings _settings;
    private readonly ILogger<DallEAppService> _logger;

    public DallEAppService(HttpClient httpClient, IOptions<OpenAISettings> settings, ILogger<DallEAppService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        var baseUrl = _settings.BaseUrl.TrimEnd('/') + "/";
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
    }

    public async Task<string> GenerateImageBase64Async(string prompt)
    {
        var truncatedPrompt = prompt.Length > 1000 ? prompt[..1000] : prompt;

        var requestBody = new
        {
            model = "dall-e-3",
            prompt = truncatedPrompt,
            n = 1,
            size = "1024x1024",
            response_format = "b64_json"
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogInformation("Generating image via DALL-E...");

        const int maxRetries = 3;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            var response = await _httpClient.PostAsync("images/generations", content);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                var delay = (int)Math.Pow(2, attempt + 1) * 10;
                _logger.LogWarning("Rate limit hit. Waiting {Delay}s before retrying ({Attempt}/{MaxRetries})...", delay, attempt + 1, maxRetries);
                await Task.Delay(TimeSpan.FromSeconds(delay));
                continue;
            }

            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var imageResponse = JsonSerializer.Deserialize<DallEResponse>(responseJson);

            var base64 = imageResponse?.Data?.FirstOrDefault()?.B64Json
                ?? throw new InvalidOperationException("Failed to generate image.");

            _logger.LogInformation("Image generated successfully");
            return base64;
        }

        throw new HttpRequestException("Rate limit exceeded after all retries.");
    }
}

#region DALL-E Response Models

internal class DallEResponse
{
    [JsonPropertyName("data")]
    public List<DallEImageData>? Data { get; set; }
}

internal class DallEImageData
{
    [JsonPropertyName("b64_json")]
    public string? B64Json { get; set; }
}

#endregion
