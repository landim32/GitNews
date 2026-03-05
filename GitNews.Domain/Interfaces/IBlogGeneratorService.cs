using GitNews.Domain.Models;

namespace GitNews.Domain.Interfaces;

public interface IBlogGeneratorService
{
    Task<BlogPost> GenerateBlogPostAsync(RepositoryContext context);
}
