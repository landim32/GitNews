using GitNews.Domain.Models;

namespace GitNews.Domain.Interfaces;

public interface IMarkdownWriter
{
    Task<string> WritePostAsync(BlogPost post);
}
