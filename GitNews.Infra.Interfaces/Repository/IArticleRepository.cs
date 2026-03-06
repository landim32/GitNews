namespace GitNews.Infra.Interfaces.Repository;

public interface IArticleRepository<TModel> where TModel : class
{
    Task<TModel> SaveAsync(TModel article);
    Task<List<TModel>> FindSimilarAsync(float[] embedding, double threshold = 0.85, int limit = 3);
}
