using GitNews.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace GitNews.Infra.Sqlite.Context;

public class GitNewsSqliteDbContext : DbContext
{
    public GitNewsSqliteDbContext(DbContextOptions<GitNewsSqliteDbContext> options) : base(options)
    {
    }

    public DbSet<ProcessedCommit> ProcessedCommits { get; set; }
    public DbSet<Article> Articles { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Article>(entity =>
        {
            entity.ToTable("articles");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id");

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

            entity.Property(e => e.ImageBase64)
                .HasColumnName("image_base64");

            entity.Property(e => e.IsProcessed)
                .HasColumnName("is_processed")
                .HasDefaultValue(false);

            // SQLite has no vector type — store as BLOB
            entity.Property(e => e.Embedding)
                .HasColumnName("embedding")
                .HasConversion(
                    v => v != null ? FloatArrayToBytes(v) : null,
                    v => v != null ? BytesToFloatArray(v) : null);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("datetime('now')");
        });

        modelBuilder.Entity<ProcessedCommit>(entity =>
        {
            entity.ToTable("processed_commits");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id");

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
                .HasDefaultValueSql("datetime('now')");

            entity.HasIndex(e => new { e.Repository, e.Sha })
                .IsUnique()
                .HasDatabaseName("ix_processed_commits_repository_sha");
        });
    }

    private static byte[] FloatArrayToBytes(float[] floats)
    {
        var bytes = new byte[floats.Length * sizeof(float)];
        Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] BytesToFloatArray(byte[] bytes)
    {
        var floats = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }
}
