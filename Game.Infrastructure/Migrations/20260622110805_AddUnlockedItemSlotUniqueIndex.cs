using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUnlockedItemSlotUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_UnlockedItems_PlayerId_EquipmentSlotId",
                table: "UnlockedItems",
                columns: new[] { "PlayerId", "EquipmentSlotId" },
                unique: true,
                filter: "\"EquipmentSlotId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UnlockedItems_PlayerId_EquipmentSlotId",
                table: "UnlockedItems");
        }
    }
}
