using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddParryRiposteIntrinsics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Attributes",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { 47, "The percentage chance to parry an incoming attack, negating it and striking back with the equipped weapon's signature skill.", "Parry Chance" },
                    { 48, "A multiplier applied to your parry chance.", "Parry Chance Multiplier" }
                });

            migrationBuilder.InsertData(
                table: "StatisticTypes",
                columns: new[] { "Id", "EntityType", "Name" },
                values: new object[,]
                {
                    { 23, 0, "Attacks Parried" },
                    { 24, 0, "Damage Parried" },
                    { 25, 0, "Counter Damage Dealt" }
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
