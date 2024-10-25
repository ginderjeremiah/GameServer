using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameInfrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InventoryItemUniqueRestraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_PlayerId",
                table: "InventoryItems");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_PlayerId_Equipped_InventorySlotNumber",
                table: "InventoryItems",
                columns: new[] { "PlayerId", "Equipped", "InventorySlotNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_PlayerId_Equipped_InventorySlotNumber",
                table: "InventoryItems");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_PlayerId",
                table: "InventoryItems",
                column: "PlayerId");
        }
    }
}
