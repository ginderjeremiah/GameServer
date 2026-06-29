using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameDefenseToToughnessAttribute : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 1,
                column: "Description",
                value: "A measure of one's resilience and physical fortitude. Contributes to maximum health and toughness.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 3,
                column: "Description",
                value: "A measure of one's speed and reflexes. Improves cooldown recovery and the chance to dodge attacks.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "Description", "Name" },
                values: new object[] { "Reduces all incoming direct damage by a percentage that grows with diminishing returns, never reaching full immunity.", "Toughness" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 1,
                column: "Description",
                value: "A measure of one's resilience and physical fortitude. Contributes to maximum health and defense.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 3,
                column: "Description",
                value: "A measure of one's speed and reflexes. Improves cooldown recovery and contributes to defense.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "Description", "Name" },
                values: new object[] { "A flat reduction applied to all incoming damage.", "Defense" });
        }
    }
}
