namespace GitNews.Infra.Interfaces.Repository;

public interface IArticleRepository<TModel> where TModel : class
{
    Task<TModel> SaveAsync(TModel article);
    Task<TModel> UpdateAsync(TModel article);
    Task<List<TModel>> FindWithoutImageAsync();
    Task<TModel?> FindOldestUnprocessedAsync();
    Task<List<TModel>> FindSimilarAsync(float[] embedding, double threshold = 0.85, int limit = 3);
}
