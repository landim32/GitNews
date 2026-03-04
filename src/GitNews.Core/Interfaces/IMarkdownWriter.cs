using GitNews.Core.Models;

namespace GitNews.Core.Interfaces;

public interface IMarkdownWriter
{
    Task<string> WritePostAsync(BlogPost post);
}
