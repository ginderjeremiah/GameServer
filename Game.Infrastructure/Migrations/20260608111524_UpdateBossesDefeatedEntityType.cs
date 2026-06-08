using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateBossesDefeatedEntityType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 2,
                column: "EntityType",
                value: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 2,
                column: "EntityType",
                value: 0);
        }
    }
}
