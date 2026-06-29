using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTypedDoTAccumulators : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "Description", "Name" },
                values: new object[] { "The amount of bleed damage taken each second from damage-over-time effects.", "Bleed Damage Per Second" });

            migrationBuilder.InsertData(
                table: "Attributes",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { 37, "The amount of poison damage taken each second from damage-over-time effects.", "Poison Damage Per Second" },
                    { 38, "The amount of burn damage taken each second from damage-over-time effects.", "Burn Damage Per Second" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 37);

            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 38);

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "Description", "Name" },
                values: new object[] { "The amount of damage taken each second from damage-over-time effects.", "Damage Taken Per Second" });
        }
    }
}
