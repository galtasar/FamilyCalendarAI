using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FamilyCalendar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMickeAndEmelie : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Children",
                columns: new[] { "Id", "Activities", "ClassGroup", "Name", "School" },
                values: new object[,]
                {
                    { new Guid("11111111-0000-0000-0000-000000000005"), null, null, "Micke", null },
                    { new Guid("11111111-0000-0000-0000-000000000006"), null, null, "Emelie", null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Children",
                keyColumn: "Id",
                keyValue: new Guid("11111111-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "Children",
                keyColumn: "Id",
                keyValue: new Guid("11111111-0000-0000-0000-000000000006"));
        }
    }
}
