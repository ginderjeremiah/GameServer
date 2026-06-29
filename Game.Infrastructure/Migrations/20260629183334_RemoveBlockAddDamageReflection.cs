using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveBlockAddDamageReflection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.InsertData(
                table: "Attributes",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[] { 39, "The percentage of a direct hit's damage returned to the attacker, ignoring their defenses.", "Damage Reflection" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 39);

            migrationBuilder.InsertData(
                table: "Attributes",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { 13, "The percentage chance to block part of the damage from an incoming attack.", "Block Chance" },
                    { 14, "A flat reduction applied to damage received when an attack is blocked.", "Block Reduction" }
                });

            migrationBuilder.InsertData(
                table: "StatisticTypes",
                columns: new[] { "Id", "EntityType", "Name" },
                values: new object[,]
                {
                    { 20, 0, "Attacks Blocked" },
                    { 21, 0, "Damage Blocked" }
                });
        }
    }
}
