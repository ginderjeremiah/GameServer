using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWeaponDamageTypeAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Attributes",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { 39, "Increases the damage your sword attacks deal.", "Sword Amplification" },
                    { 40, "Increases the damage your axe attacks deal.", "Axe Amplification" },
                    { 41, "Increases the damage your bow attacks deal.", "Bow Amplification" },
                    { 42, "Increases the damage your club attacks deal.", "Club Amplification" },
                    { 43, "Increases the damage your dagger attacks deal.", "Dagger Amplification" },
                    { 44, "Increases the damage your unarmed attacks deal.", "Unarmed Amplification" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 39);

            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 41);

            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 43);

            migrationBuilder.DeleteData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 44);
        }
    }
}
