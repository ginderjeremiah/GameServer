using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RestrictAppliedModItemModSlotDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppliedMods_ItemModSlots_ItemModSlotId",
                table: "AppliedMods");

            migrationBuilder.AddForeignKey(
                name: "FK_AppliedMods_ItemModSlots_ItemModSlotId",
                table: "AppliedMods",
                column: "ItemModSlotId",
                principalTable: "ItemModSlots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppliedMods_ItemModSlots_ItemModSlotId",
                table: "AppliedMods");

            migrationBuilder.AddForeignKey(
                name: "FK_AppliedMods_ItemModSlots_ItemModSlotId",
                table: "AppliedMods",
                column: "ItemModSlotId",
                principalTable: "ItemModSlots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
