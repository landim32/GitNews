using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GitNews.Infra.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "articles",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    content = table.Column<string>(type: "TEXT", nullable: false),
                    category = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    tags = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    repository = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    author = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    embedding = table.Column<byte[]>(type: "BLOB", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_articles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "processed_commits",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    repository = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    sha = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    processed_at = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processed_commits", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_processed_commits_repository_sha",
                table: "processed_commits",
                columns: new[] { "repository", "sha" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "articles");

            migrationBuilder.DropTable(
                name: "processed_commits");
        }
    }
}
