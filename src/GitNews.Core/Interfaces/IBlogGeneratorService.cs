using GitNews.Core.Models;

namespace GitNews.Core.Interfaces;

public interface IBlogGeneratorService
{
    Task<BlogPost> GenerateBlogPostAsync(RepositoryContext context);
}
