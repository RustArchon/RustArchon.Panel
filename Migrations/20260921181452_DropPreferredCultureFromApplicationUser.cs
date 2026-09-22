using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RustArchon.Panel.Migrations
{
    /// <inheritdoc />
    public partial class DropPreferredCultureFromApplicationUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LegacyUserCultures",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Culture = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyUserCultures", x => x.UserId);
                });

            // The language moved to the Api's UserProfile, which this database cannot write to. Copy what is here to a holding table before the column goes,
            // so nobody who chose a language loses it: the Panel's UserProfileBackfillService hands these to the Api and deletes each row as the Api takes it.
            migrationBuilder.Sql(
                "INSERT INTO \"LegacyUserCultures\" (\"UserId\", \"Culture\") " +
                "SELECT \"Id\", \"PreferredCulture\" FROM \"AspNetUsers\" WHERE \"PreferredCulture\" IS NOT NULL AND \"PreferredCulture\" <> ''");

            migrationBuilder.DropColumn(
                name: "PreferredCulture",
                table: "AspNetUsers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreferredCulture",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            // Only the languages not yet handed to the Api come back; anything already handed over lives in the Api's UserProfile and is not restored here.
            migrationBuilder.Sql(
                "UPDATE \"AspNetUsers\" u SET \"PreferredCulture\" = l.\"Culture\" FROM \"LegacyUserCultures\" l WHERE l.\"UserId\" = u.\"Id\"");

            migrationBuilder.DropTable(
                name: "LegacyUserCultures");
        }
    }
}
