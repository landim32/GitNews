using GitNews.Domain.Models;
using GitNews.Infra.Context;
using GitNews.Infra.Interfaces.Repository;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace GitNews.Infra.Repository;

public class ArticleRepository : IArticleRepository<Article>
{
    private readonly GitNewsDbContext _context;

    public ArticleRepository(GitNewsDbContext context)
    {
        _context = context;
    }

    public async Task<Article> SaveAsync(Article article)
    {
        _context.Articles.Add(article);
        await _context.SaveChangesAsync();
        return article;
    }

    public async Task<Article> UpdateAsync(Article article)
    {
        _context.Articles.Update(article);
        await _context.SaveChangesAsync();
        return article;
    }

    public async Task<List<Article>> FindWithoutImageAsync()
    {
        return await _context.Articles
            .Where(a => a.ImageBase64 == null || a.ImageBase64 == "")
            .ToListAsync();
    }

    public async Task<Article?> FindOldestUnprocessedAsync()
    {
        return await _context.Articles
            .Where(a => !a.IsProcessed)
            .OrderBy(a => a.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<List<Article>> FindSimilarAsync(float[] embedding, double threshold = 0.85, int limit = 3)
    {
        var vector = new Vector(embedding);

        // EF Core traduz CosineDistance para SQL usando pgvector
        // Precisamos usar um campo shadow ou raw SQL pois o model usa float[]
        return await _context.Articles
            .FromSqlInterpolated($@"
                SELECT * FROM articles
                WHERE embedding IS NOT NULL
                  AND 1 - (embedding <=> {vector}) >= {threshold}
                ORDER BY embedding <=> {vector}
                LIMIT {limit}")
            .ToListAsync();
    }
}
