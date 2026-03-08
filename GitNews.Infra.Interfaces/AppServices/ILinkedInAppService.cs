namespace GitNews.Infra.Interfaces.AppServices;

public interface ILinkedInAppService
{
    Task EnsureLoggedInAsync(CancellationToken cancellationToken = default);
    Task<string> PublishArticleAsync(string title, string markdownContent, string[] tags, byte[]? coverImage = null, CancellationToken cancellationToken = default);
}
