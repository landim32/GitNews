namespace GitNews.Infra.Interfaces.AppServices;

public interface ICodeImageAppService
{
    Task<byte[]> GenerateImageFromMarkdownAsync(string markdownContent);
}
