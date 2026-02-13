using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SC701.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSingleSessionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrentSessionId",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActivityAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentSessionId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastActivityAt",
                table: "AspNetUsers");
        }
    }
}
