using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyCalendar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexesAndConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Emails_Classification",
                table: "Emails",
                column: "Classification");

            migrationBuilder.CreateIndex(
                name: "IX_Emails_MessageId",
                table: "Emails",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_ChildName",
                table: "CalendarEvents",
                column: "ChildName");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_StartTime",
                table: "CalendarEvents",
                column: "StartTime");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Emails_Classification",
                table: "Emails");

            migrationBuilder.DropIndex(
                name: "IX_Emails_MessageId",
                table: "Emails");

            migrationBuilder.DropIndex(
                name: "IX_CalendarEvents_ChildName",
                table: "CalendarEvents");

            migrationBuilder.DropIndex(
                name: "IX_CalendarEvents_StartTime",
                table: "CalendarEvents");
        }
    }
}
