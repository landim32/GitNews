using GitNews.Domain.Models;
using GitNews.Infra.Interfaces.Repository;
using GitNews.Infra.Sqlite.Context;
using Microsoft.EntityFrameworkCore;

namespace GitNews.Infra.Sqlite.Repository;

public class ArticleRepository : IArticleRepository<Article>
{
    private readonly GitNewsSqliteDbContext _context;

    public ArticleRepository(GitNewsSqliteDbContext context)
    {
        _context = context;
    }

    public async Task<Article> SaveAsync(Article article)
    {
        _context.Articles.Add(article);
        await _context.SaveChangesAsync();
        return article;
    }

    public async Task<List<Article>> FindSimilarAsync(float[] embedding, double threshold = 0.85, int limit = 3)
    {
        // SQLite has no vector operations — load embeddings and compute cosine similarity in memory
        var articles = await _context.Articles
            .AsNoTracking()
            .Where(a => a.Embedding != null)
            .ToListAsync();

        return articles
            .Select(a => new { Article = a, Similarity = CosineSimilarity(embedding, a.Embedding!) })
            .Where(x => x.Similarity >= threshold)
            .OrderByDescending(x => x.Similarity)
            .Take(limit)
            .Select(x => x.Article)
            .ToList();
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        double dotProduct = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denominator = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denominator == 0 ? 0 : dotProduct / denominator;
    }
}
