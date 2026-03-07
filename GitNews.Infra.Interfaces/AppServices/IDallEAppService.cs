namespace GitNews.Infra.Interfaces.AppServices;

public interface IDallEAppService
{
    Task<string> GenerateImageBase64Async(string prompt);
}
