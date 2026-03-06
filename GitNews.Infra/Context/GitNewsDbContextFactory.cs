using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace GitNews.Infra.Context;

public class GitNewsDbContextFactory : IDesignTimeDbContextFactory<GitNewsDbContext>
{
    public GitNewsDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "GitNews.Console"))
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var connectionString = configuration["Database:ConnectionString"]
            ?? throw new InvalidOperationException("Connection string 'Database:ConnectionString' não encontrada no appsettings.json.");

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseVector();
        var dataSource = dataSourceBuilder.Build();

        var optionsBuilder = new DbContextOptionsBuilder<GitNewsDbContext>();
        optionsBuilder.UseNpgsql(dataSource, o => o.UseVector());

        return new GitNewsDbContext(optionsBuilder.Options);
    }
}
