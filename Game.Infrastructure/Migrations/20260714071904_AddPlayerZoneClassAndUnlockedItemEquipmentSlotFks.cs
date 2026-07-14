using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerZoneClassAndUnlockedItemEquipmentSlotFks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_UnlockedItems_EquipmentSlotId",
                table: "UnlockedItems",
                column: "EquipmentSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_ClassId",
                table: "Players",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_CurrentZoneId",
                table: "Players",
                column: "CurrentZoneId");

            migrationBuilder.AddForeignKey(
                name: "FK_Players_Classes_ClassId",
                table: "Players",
                column: "ClassId",
                principalTable: "Classes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Players_Zones_CurrentZoneId",
                table: "Players",
                column: "CurrentZoneId",
                principalTable: "Zones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UnlockedItems_EquipmentSlots_EquipmentSlotId",
                table: "UnlockedItems",
                column: "EquipmentSlotId",
                principalTable: "EquipmentSlots",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Players_Classes_ClassId",
                table: "Players");

            migrationBuilder.DropForeignKey(
                name: "FK_Players_Zones_CurrentZoneId",
                table: "Players");

            migrationBuilder.DropForeignKey(
                name: "FK_UnlockedItems_EquipmentSlots_EquipmentSlotId",
                table: "UnlockedItems");

            migrationBuilder.DropIndex(
                name: "IX_UnlockedItems_EquipmentSlotId",
                table: "UnlockedItems");

            migrationBuilder.DropIndex(
                name: "IX_Players_ClassId",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Players_CurrentZoneId",
                table: "Players");
        }
    }
}
