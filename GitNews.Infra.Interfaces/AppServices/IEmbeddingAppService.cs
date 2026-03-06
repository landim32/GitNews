namespace GitNews.Infra.Interfaces.AppServices;

public interface IEmbeddingAppService
{
    Task<float[]> GenerateEmbeddingAsync(string text);
}
