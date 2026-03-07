using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GitNews.Infra.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddArticleIsProcessed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_processed",
                table: "articles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_processed",
                table: "articles");
        }
    }
}
