using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDodgeChanceMultiplierIntrinsic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 3,
                column: "Description",
                value: "A measure of one's speed and reflexes. Improves cooldown recovery and amplifies dodge chance.");

            migrationBuilder.InsertData(
                table: "Attributes",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[] { 49, "A multiplier applied to your dodge chance.", "Dodge Chance Multiplier" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 49);

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 3,
                column: "Description",
                value: "A measure of one's speed and reflexes. Improves cooldown recovery and the chance to dodge attacks.");
        }
    }
}
