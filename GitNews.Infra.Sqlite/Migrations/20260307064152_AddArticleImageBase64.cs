using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GitNews.Infra.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddArticleImageBase64 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "image_base64",
                table: "articles",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "image_base64",
                table: "articles");
        }
    }
}
