using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyCalendar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHasTimeAndSyncError : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasTime",
                table: "CalendarEvents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SyncError",
                table: "CalendarEvents",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasTime",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "SyncError",
                table: "CalendarEvents");
        }
    }
}
