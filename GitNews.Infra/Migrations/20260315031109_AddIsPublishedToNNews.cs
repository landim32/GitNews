using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GitNews.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddIsPublishedToNNews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_published_to_nnews",
                table: "articles",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_published_to_nnews",
                table: "articles");
        }
    }
}
