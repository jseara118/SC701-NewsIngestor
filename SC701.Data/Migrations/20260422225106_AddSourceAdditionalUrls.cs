using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SC701.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceAdditionalUrls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdditionalUrls",
                table: "Sources",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdditionalUrls",
                table: "Sources");
        }
    }
}
