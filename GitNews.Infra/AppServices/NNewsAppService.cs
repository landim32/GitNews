using GitNews.DTO;
using GitNews.Infra.Interfaces.AppServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NAuth.ACL.Interfaces;
using NAuth.DTO;
using NNews.ACL.Interfaces;
using NNews.DTO;

namespace GitNews.Infra.AppServices;

public class NNewsAppService : INNewsAppService
{
    private readonly IArticleClient _articleClient;
    private readonly IUserClient _userClient;
    private readonly NNewsSettings _settings;
    private readonly ILogger<NNewsAppService> _logger;
    private string? _cachedToken;

    public NNewsAppService(
        IArticleClient articleClient,
        IUserClient userClient,
        IOptions<NNewsSettings> settings,
        ILogger<NNewsAppService> logger)
    {
        _articleClient = articleClient;
        _userClient = userClient;
        _settings = settings.Value;
        _logger = logger;
    }

    private async Task<string> GetTokenAsync()
    {
        if (!string.IsNullOrEmpty(_cachedToken))
            return _cachedToken;

        _logger.LogInformation("Authenticating with NAuth...");

        var loginParam = new LoginParam
        {
            Email = _settings.Email,
            Password = _settings.Password
        };

        var result = await _userClient.LoginWithEmailAsync(loginParam);
        if (result == null || string.IsNullOrEmpty(result.Token))
            throw new InvalidOperationException("Failed to authenticate with NAuth. Check your email and password settings.");

        _cachedToken = result.Token;
        _logger.LogInformation("Successfully authenticated with NAuth");

        return _cachedToken;
    }

    public async Task<string> PublishArticleAsync(
        string title,
        string markdownContent,
        string category,
        string[] tags,
        byte[]? coverImage = null,
        CancellationToken cancellationToken = default)
    {
        var token = await GetTokenAsync();

        _logger.LogInformation("Publishing article to NNews: {Title}", title);

        var articleInserted = new ArticleInsertedInfo
        {
            Title = title,
            Content = markdownContent,
            TagList = string.Join(",", tags),
            Status = 1 // Published
        };

        var article = await _articleClient.CreateAsync(articleInserted, token);
        if (article == null)
            throw new InvalidOperationException("Failed to create article on NNews API.");

        _logger.LogInformation("Article published on NNews with ID: {ArticleId}", article.ArticleId);

        return article.ArticleId.ToString();
    }
}
