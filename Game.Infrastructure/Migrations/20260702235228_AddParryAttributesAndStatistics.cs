using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddParryAttributesAndStatistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Attributes",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { 47, "The percentage chance to fully avoid an incoming attack and counterattack with your weapon's signature skill.", "Parry Chance" },
                    { 48, "A multiplier applied to your base chance to parry.", "Parry Chance Multiplier" }
                });

            migrationBuilder.InsertData(
                table: "StatisticTypes",
                columns: new[] { "Id", "EntityType", "Name" },
                values: new object[,]
                {
                    { 23, 0, "Attacks Parried" },
                    { 24, 0, "Counter Damage Dealt" },
                    { 25, 0, "Damage Parried" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 47);

            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 48);

            migrationBuilder.DeleteData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 25);
        }
    }
}
