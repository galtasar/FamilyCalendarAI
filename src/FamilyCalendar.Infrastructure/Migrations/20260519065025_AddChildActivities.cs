using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyCalendar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChildActivities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Activities",
                table: "Children",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewQuestionsJson",
                table: "CalendarEvents",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Children",
                keyColumn: "Id",
                keyValue: new Guid("11111111-0000-0000-0000-000000000001"),
                column: "Activities",
                value: null);

            migrationBuilder.UpdateData(
                table: "Children",
                keyColumn: "Id",
                keyValue: new Guid("11111111-0000-0000-0000-000000000002"),
                column: "Activities",
                value: null);

            migrationBuilder.UpdateData(
                table: "Children",
                keyColumn: "Id",
                keyValue: new Guid("11111111-0000-0000-0000-000000000003"),
                column: "Activities",
                value: null);

            migrationBuilder.UpdateData(
                table: "Children",
                keyColumn: "Id",
                keyValue: new Guid("11111111-0000-0000-0000-000000000004"),
                column: "Activities",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Activities",
                table: "Children");

            migrationBuilder.DropColumn(
                name: "ReviewQuestionsJson",
                table: "CalendarEvents");
        }
    }
}
