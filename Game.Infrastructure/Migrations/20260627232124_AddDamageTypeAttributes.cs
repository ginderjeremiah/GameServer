using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDamageTypeAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Attributes",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { 17, "Increases the damage your physical attacks deal.", "Physical Amplification" },
                    { 18, "Reduces the damage you take from physical attacks.", "Physical Resistance" },
                    { 19, "Increases the damage your fire attacks deal.", "Fire Amplification" },
                    { 20, "Reduces the damage you take from fire attacks.", "Fire Resistance" },
                    { 21, "Increases the damage your water attacks deal.", "Water Amplification" },
                    { 22, "Reduces the damage you take from water attacks.", "Water Resistance" },
                    { 23, "Increases the damage your earth attacks deal.", "Earth Amplification" },
                    { 24, "Reduces the damage you take from earth attacks.", "Earth Resistance" },
                    { 25, "Increases the damage your wind attacks deal.", "Wind Amplification" },
                    { 26, "Reduces the damage you take from wind attacks.", "Wind Resistance" },
                    { 27, "Increases the damage your bleed attacks deal.", "Bleed Amplification" },
                    { 28, "Reduces the damage you take from bleed attacks.", "Bleed Resistance" },
                    { 29, "Increases the damage your poison attacks deal.", "Poison Amplification" },
                    { 30, "Reduces the damage you take from poison attacks.", "Poison Resistance" },
                    { 31, "Increases the damage your burn attacks deal.", "Burn Amplification" },
                    { 32, "Reduces the damage you take from burn attacks.", "Burn Resistance" },
                    { 33, "Increases the damage your elemental attacks deal.", "Elemental Amplification" },
                    { 34, "Reduces the damage you take from elemental attacks.", "Elemental Resistance" },
                    { 35, "Increases the damage your damage-over-time attacks deal.", "Dot Amplification" },
                    { 36, "Reduces the damage you take from damage-over-time attacks.", "Dot Resistance" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 36);
        }
    }
}
