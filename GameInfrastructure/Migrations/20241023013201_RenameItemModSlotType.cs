using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameInfrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameItemModSlotType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItemMods_ItemSlots_ItemSlotId",
                table: "InventoryItemMods");

            migrationBuilder.DropTable(
                name: "ItemSlots");

            migrationBuilder.RenameColumn(
                name: "ItemSlotId",
                table: "InventoryItemMods",
                newName: "ItemModSlotId");

            migrationBuilder.RenameIndex(
                name: "IX_InventoryItemMods_ItemSlotId",
                table: "InventoryItemMods",
                newName: "IX_InventoryItemMods_ItemModSlotId");

            migrationBuilder.CreateTable(
                name: "ItemModSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    ItemModSlotTypeId = table.Column<int>(type: "int", nullable: false),
                    GuaranteedItemModId = table.Column<int>(type: "int", nullable: true),
                    Probability = table.Column<decimal>(type: "decimal(9,8)", precision: 9, scale: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemModSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemModSlots_ItemMods_GuaranteedItemModId",
                        column: x => x.GuaranteedItemModId,
                        principalTable: "ItemMods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItemModSlots_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemModSlots_SlotTypes_ItemModSlotTypeId",
                        column: x => x.ItemModSlotTypeId,
                        principalTable: "SlotTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItemModSlots_GuaranteedItemModId",
                table: "ItemModSlots",
                column: "GuaranteedItemModId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemModSlots_ItemId",
                table: "ItemModSlots",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemModSlots_ItemModSlotTypeId",
                table: "ItemModSlots",
                column: "ItemModSlotTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItemMods_ItemModSlots_ItemModSlotId",
                table: "InventoryItemMods",
                column: "ItemModSlotId",
                principalTable: "ItemModSlots",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItemMods_ItemModSlots_ItemModSlotId",
                table: "InventoryItemMods");

            migrationBuilder.DropTable(
                name: "ItemModSlots");

            migrationBuilder.RenameColumn(
                name: "ItemModSlotId",
                table: "InventoryItemMods",
                newName: "ItemSlotId");

            migrationBuilder.RenameIndex(
                name: "IX_InventoryItemMods_ItemModSlotId",
                table: "InventoryItemMods",
                newName: "IX_InventoryItemMods_ItemSlotId");

            migrationBuilder.CreateTable(
                name: "ItemSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GuaranteedItemModId = table.Column<int>(type: "int", nullable: true),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    SlotTypeId = table.Column<int>(type: "int", nullable: false),
                    Probability = table.Column<decimal>(type: "decimal(9,8)", precision: 9, scale: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemSlots_ItemMods_GuaranteedItemModId",
                        column: x => x.GuaranteedItemModId,
                        principalTable: "ItemMods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItemSlots_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemSlots_SlotTypes_SlotTypeId",
                        column: x => x.SlotTypeId,
                        principalTable: "SlotTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItemSlots_GuaranteedItemModId",
                table: "ItemSlots",
                column: "GuaranteedItemModId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSlots_ItemId",
                table: "ItemSlots",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSlots_SlotTypeId",
                table: "ItemSlots",
                column: "SlotTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItemMods_ItemSlots_ItemSlotId",
                table: "InventoryItemMods",
                column: "ItemSlotId",
                principalTable: "ItemSlots",
                principalColumn: "Id");
        }
    }
}
