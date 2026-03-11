using GitNews.Infra.Interfaces.AppServices;
using Microsoft.Extensions.Logging;
using NNews.ACL.Interfaces;
using NNews.DTO;

namespace GitNews.Infra.AppServices;

public class NNewsAppService : INNewsAppService
{
    private readonly IArticleClient _articleClient;
    private readonly ILogger<NNewsAppService> _logger;

    public NNewsAppService(
        IArticleClient articleClient,
        ILogger<NNewsAppService> logger)
    {
        _articleClient = articleClient;
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

        var articleInserted = new ArticleInsertedInfo
        {
            Title = title,
            Content = markdownContent,
            TagList = string.Join(",", tags),
            Status = 1 // Published
        };

        var article = await _articleClient.CreateAsync(articleInserted, cancellationToken);
        if (article == null)
            throw new InvalidOperationException("Failed to create article on NNews API.");

        _logger.LogInformation("Article published on NNews with ID: {ArticleId}", article.ArticleId);

        return article.ArticleId.ToString();
    }
}
