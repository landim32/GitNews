namespace GitNews.Infra.Interfaces.AppServices;

public interface INNewsAppService
{
    Task<string> PublishArticleAsync(string title, string markdownContent, string category, string[] tags, byte[]? coverImage = null, CancellationToken cancellationToken = default);
}
