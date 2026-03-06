using GitNews.DTO;

namespace GitNews.Infra.Interfaces.AppServices;

public interface IBlogGeneratorAppService
{
    Task<BlogPostInfo> GenerateBlogPostAsync(RepositoryContextInfo context);
}
