using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProficiencyLogType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "LogTypes",
                columns: new[] { "Id", "DefaultValue", "Name" },
                values: new object[] { 8, false, "Proficiency" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "LogTypes",
                keyColumn: "Id",
                keyValue: 8);
        }
    }
}
