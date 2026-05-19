using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyCalendar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyChildToDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Activities",
                table: "Children");

            migrationBuilder.DropColumn(
                name: "ClassGroup",
                table: "Children");

            migrationBuilder.RenameColumn(
                name: "School",
                table: "Children",
                newName: "Description");

            migrationBuilder.UpdateData(
                table: "Children",
                keyColumn: "Id",
                keyValue: new Guid("11111111-0000-0000-0000-000000000001"),
                column: "Description",
                value: "Går i klass 5 på Vattholmaskolan.");

            migrationBuilder.UpdateData(
                table: "Children",
                keyColumn: "Id",
                keyValue: new Guid("11111111-0000-0000-0000-000000000002"),
                column: "Description",
                value: "Går i klass 3 på Vattholmaskolan.");

            migrationBuilder.UpdateData(
                table: "Children",
                keyColumn: "Id",
                keyValue: new Guid("11111111-0000-0000-0000-000000000003"),
                column: "Description",
                value: "Går på Hyttans förskola.");

            migrationBuilder.UpdateData(
                table: "Children",
                keyColumn: "Id",
                keyValue: new Guid("11111111-0000-0000-0000-000000000004"),
                column: "Description",
                value: "Går på Hyttans förskola.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Description",
                table: "Children",
                newName: "School");

            migrationBuilder.AddColumn<string>(
                name: "Activities",
                table: "Children",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClassGroup",
                table: "Children",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Children",
                keyColumn: "Id",
                keyValue: new Guid("11111111-0000-0000-0000-000000000001"),
                columns: new[] { "Activities", "ClassGroup", "School" },
                values: new object[] { null, "Klass 5", "Vattholmaskolan" });

            migrationBuilder.UpdateData(
                table: "Children",
                keyColumn: "Id",
                keyValue: new Guid("11111111-0000-0000-0000-000000000002"),
                columns: new[] { "Activities", "ClassGroup", "School" },
                values: new object[] { null, "Klass 3", "Vattholmaskolan" });

            migrationBuilder.UpdateData(
                table: "Children",
                keyColumn: "Id",
                keyValue: new Guid("11111111-0000-0000-0000-000000000003"),
                columns: new[] { "Activities", "ClassGroup", "School" },
                values: new object[] { null, "Förskola", "Hyttans förskola" });

            migrationBuilder.UpdateData(
                table: "Children",
                keyColumn: "Id",
                keyValue: new Guid("11111111-0000-0000-0000-000000000004"),
                columns: new[] { "Activities", "ClassGroup", "School" },
                values: new object[] { null, "Förskola", "Hyttans förskola" });

            migrationBuilder.UpdateData(
                table: "Children",
                keyColumn: "Id",
                keyValue: new Guid("11111111-0000-0000-0000-000000000005"),
                columns: new[] { "Activities", "ClassGroup" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Children",
                keyColumn: "Id",
                keyValue: new Guid("11111111-0000-0000-0000-000000000006"),
                columns: new[] { "Activities", "ClassGroup" },
                values: new object[] { null, null });
        }
    }
}
