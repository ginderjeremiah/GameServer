using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCooldownBonusCadenceIntrinsics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 3,
                column: "Description",
                value: "A measure of one's speed and reflexes. Amplifies your cooldown bonus and dodge chance.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 4,
                column: "Description",
                value: "A measure of one's precision and finesse. Increases the damage of some physical skills.");

            migrationBuilder.InsertData(
                table: "Attributes",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { 50, "Additional cooldown recovery granted by items and skill effects, amplified by your cooldown bonus multiplier.", "Cooldown Bonus" },
                    { 51, "A multiplier applied to your cooldown bonus.", "Cooldown Bonus Multiplier" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 50);

            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 51);

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 3,
                column: "Description",
                value: "A measure of one's speed and reflexes. Improves cooldown recovery and amplifies dodge chance.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 4,
                column: "Description",
                value: "A measure of one's precision and finesse. Increases the damage of some physical skills and improves cooldown recovery.");
        }
    }
}
