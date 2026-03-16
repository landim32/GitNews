using GitNews.DTO;
using GitNews.Infra.Interfaces.AppServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NNews.ACL.Interfaces;
using NNews.DTO;

namespace GitNews.Infra.AppServices;

public class NNewsAppService : INNewsAppService
{
    private readonly IArticleClient _articleClient;
    private readonly ICategoryClient _categoryClient;
    private readonly HttpClient _httpClient;
    private readonly NNewsSettings _settings;
    private readonly ILogger<NNewsAppService> _logger;

    public NNewsAppService(
        IArticleClient articleClient,
        ICategoryClient categoryClient,
        HttpClient httpClient,
        IOptions<NNewsSettings> settings,
        ILogger<NNewsAppService> logger)
    {
        _articleClient = articleClient;
        _categoryClient = categoryClient;
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<string> PublishArticleAsync(
        string title,
        string markdownContent,
        string category,
        string[] tags,
        byte[]? coverImage = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Publishing article to NNews: {Title}", title);

        var categoryId = await GetOrCreateCategoryAsync(category, cancellationToken);

        string? imageName = null;
        if (coverImage != null && coverImage.Length > 0)
            imageName = await UploadImageAsync(coverImage, cancellationToken);

        var articleInserted = new ArticleInsertedInfo
        {
            Title = title,
            Content = markdownContent,
            CategoryId = categoryId,
            ImageName = imageName,
            TagList = string.Join(",", tags),
            Status = 1, // Published
            ContentType = 3, // Markdown
            DateAt = DateTime.UtcNow
        };

        var article = await _articleClient.CreateAsync(articleInserted, cancellationToken);
        if (article == null)
            throw new InvalidOperationException("Failed to create article on NNews API.");

        _logger.LogInformation("Article published on NNews with ID: {ArticleId}", article.ArticleId);

        return article.ArticleId.ToString();
    }

    private async Task<string> UploadImageAsync(byte[] imageBytes, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Uploading cover image to NNews ({Size} bytes)", imageBytes.Length);

        var compressed = CompressImage(imageBytes);
        _logger.LogInformation("Image compressed to {Size} bytes", compressed.Length);

        var apiUrl = _settings.ApiUrl.TrimEnd('/');
        using var content = new MultipartFormDataContent();
        using var imageContent = new ByteArrayContent(compressed);
        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        content.Add(imageContent, "file", "cover.jpg");

        var response = await _httpClient.PostAsync($"{apiUrl}/Image/uploadImage", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var imageUrl = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogInformation("Cover image uploaded: {ImageUrl}", imageUrl);

        return imageUrl.Trim('"');
    }

    private static byte[] CompressImage(byte[] imageBytes)
    {
        using var original = SkiaSharp.SKBitmap.Decode(imageBytes);
        var maxWidth = 800;
        if (original.Width > maxWidth)
        {
            var ratio = (float)maxWidth / original.Width;
            var newHeight = (int)(original.Height * ratio);
            using var resized = original.Resize(new SkiaSharp.SKImageInfo(maxWidth, newHeight), SkiaSharp.SKFilterQuality.Medium);
            using var image = SkiaSharp.SKImage.FromBitmap(resized);
            using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 75);
            return data.ToArray();
        }

        using var img = SkiaSharp.SKImage.FromBitmap(original);
        using var encoded = img.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 75);
        return encoded.ToArray();
    }

    private async Task<long> GetOrCreateCategoryAsync(string categoryTitle, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(categoryTitle))
            categoryTitle = "Geral";

        var categories = await _categoryClient.GetAllAsync(cancellationToken);
        var existing = categories.FirstOrDefault(c =>
            string.Equals(c.Title, categoryTitle, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            _logger.LogInformation("Category found: {Category} (ID: {CategoryId})", existing.Title, existing.CategoryId);
            return existing.CategoryId;
        }

        _logger.LogInformation("Category not found, creating: {Category}", categoryTitle);
        var newCategory = await _categoryClient.CreateAsync(new CategoryInfo
        {
            Title = categoryTitle
        }, cancellationToken);

        _logger.LogInformation("Category created: {Category} (ID: {CategoryId})", newCategory.Title, newCategory.CategoryId);
        return newCategory.CategoryId;
    }
}
