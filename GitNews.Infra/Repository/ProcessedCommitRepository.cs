using GitNews.Infra.Interfaces.Repository;
using GitNews.Domain.Models;
using GitNews.Infra.Context;
using Microsoft.EntityFrameworkCore;

namespace GitNews.Infra.Repository;

public class ProcessedCommitRepository : IProcessedCommitRepository<ProcessedCommit>
{
    private readonly GitNewsDbContext _context;

    public ProcessedCommitRepository(GitNewsDbContext context)
    {
        _context = context;
    }

    public async Task<bool> IsCommitProcessedAsync(string repository, string sha)
    {
        return await _context.ProcessedCommits
            .AsNoTracking()
            .AnyAsync(c => c.Repository == repository && c.Sha == sha);
    }

    public async Task MarkAsProcessedAsync(string repository, string sha)
    {
        var exists = await IsCommitProcessedAsync(repository, sha);
        if (exists) return;

        _context.ProcessedCommits.Add(new ProcessedCommit
        {
            Repository = repository,
            Sha = sha,
            ProcessedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
    }

    public async Task MarkAsProcessedAsync(string repository, IEnumerable<string> shas)
    {
        var existingShas = await _context.ProcessedCommits
            .AsNoTracking()
            .Where(c => c.Repository == repository && shas.Contains(c.Sha))
            .Select(c => c.Sha)
            .ToListAsync();

        var newShas = shas.Except(existingShas);

        foreach (var sha in newShas)
        {
            _context.ProcessedCommits.Add(new ProcessedCommit
            {
                Repository = repository,
                Sha = sha,
                ProcessedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
    }
}
