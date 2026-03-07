using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace GitNews.Infra.Sqlite.Context;

public class GitNewsSqliteDbContextFactory : IDesignTimeDbContextFactory<GitNewsSqliteDbContext>
{
    public GitNewsSqliteDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "GitNews.Console"))
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var connectionString = configuration["Database:ConnectionString"]
            ?? "Data Source=gitnews.db";

        var optionsBuilder = new DbContextOptionsBuilder<GitNewsSqliteDbContext>();
        optionsBuilder.UseSqlite(connectionString);

        return new GitNewsSqliteDbContext(optionsBuilder.Options);
    }
}
