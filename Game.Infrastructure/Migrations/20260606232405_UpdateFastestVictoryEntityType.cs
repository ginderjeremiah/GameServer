using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFastestVictoryEntityType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 13,
                column: "EntityType",
                value: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 13,
                column: "EntityType",
                value: 0);
        }
    }
}
