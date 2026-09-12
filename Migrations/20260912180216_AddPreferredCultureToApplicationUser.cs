using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RustArchon.Panel.Migrations
{
    /// <inheritdoc />
    public partial class AddPreferredCultureToApplicationUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreferredCulture",
                table: "AspNetUsers",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreferredCulture",
                table: "AspNetUsers");
        }
    }
}
