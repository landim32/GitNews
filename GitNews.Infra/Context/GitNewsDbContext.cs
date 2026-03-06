using GitNews.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pgvector;

namespace GitNews.Infra.Context;

public class GitNewsDbContext : DbContext
{
    public GitNewsDbContext(DbContextOptions<GitNewsDbContext> options) : base(options)
    {
    }

    public DbSet<ProcessedCommit> ProcessedCommits { get; set; }
    public DbSet<Article> Articles { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        var vectorConverter = new ValueConverter<float[]?, Vector>(
            v => v != null ? new Vector(v) : new Vector(Array.Empty<float>()),
            v => v.ToArray());

        modelBuilder.Entity<Article>(entity =>
        {
            entity.ToTable("articles");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .UseIdentityAlwaysColumn();

            entity.Property(e => e.Title)
                .HasColumnName("title")
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(e => e.Content)
                .HasColumnName("content")
                .IsRequired();

            entity.Property(e => e.Category)
                .HasColumnName("category")
                .HasMaxLength(200);

            entity.Property(e => e.Tags)
                .HasColumnName("tags")
                .HasMaxLength(1000);

            entity.Property(e => e.Repository)
                .HasColumnName("repository")
                .HasMaxLength(500);

            entity.Property(e => e.Author)
                .HasColumnName("author")
                .HasMaxLength(200);

            entity.Property(e => e.Slug)
                .HasColumnName("slug")
                .HasMaxLength(500);

            entity.Property(e => e.Embedding)
                .HasColumnName("embedding")
                .HasColumnType("vector(1536)")
                .HasConversion(vectorConverter);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<ProcessedCommit>(entity =>
        {
            entity.ToTable("processed_commits");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .UseIdentityAlwaysColumn();

            entity.Property(e => e.Repository)
                .HasColumnName("repository")
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(e => e.Sha)
                .HasColumnName("sha")
                .HasMaxLength(40)
                .IsRequired();

            entity.Property(e => e.ProcessedAt)
                .HasColumnName("processed_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("now()");

            entity.HasIndex(e => new { e.Repository, e.Sha })
                .IsUnique()
                .HasDatabaseName("ix_processed_commits_repository_sha");
        });
    }
}
