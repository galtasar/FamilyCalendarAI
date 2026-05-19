using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FamilyCalendar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameChildToFamilyMember : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "Children",
                newName: "FamilyMembers");

            migrationBuilder.RenameColumn(
                name: "ChildName",
                table: "CalendarEvents",
                newName: "FamilyMemberName");

            migrationBuilder.RenameIndex(
                name: "IX_CalendarEvents_ChildName",
                table: "CalendarEvents",
                newName: "IX_CalendarEvents_FamilyMemberName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "FamilyMembers",
                newName: "Children");

            migrationBuilder.RenameColumn(
                name: "FamilyMemberName",
                table: "CalendarEvents",
                newName: "ChildName");

            migrationBuilder.RenameIndex(
                name: "IX_CalendarEvents_FamilyMemberName",
                table: "CalendarEvents",
                newName: "IX_CalendarEvents_ChildName");
        }
    }
}
