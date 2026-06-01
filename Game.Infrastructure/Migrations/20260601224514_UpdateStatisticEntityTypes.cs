using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateStatisticEntityTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 4,
                column: "EntityType",
                value: 3);

            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 5,
                column: "EntityType",
                value: 3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 4,
                column: "EntityType",
                value: 0);

            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 5,
                column: "EntityType",
                value: 0);
        }
    }
}
