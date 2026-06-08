using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBattlesAbandonedStatistic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "StatisticTypes",
                columns: new[] { "Id", "EntityType", "Name" },
                values: new object[] { 15, 1, "Battles Abandoned" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 15);
        }
    }
}
