using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddItemModRarity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RarityId",
                table: "ItemMods",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_ItemMods_RarityId",
                table: "ItemMods",
                column: "RarityId");

            migrationBuilder.AddForeignKey(
                name: "FK_ItemMods_Rarities_RarityId",
                table: "ItemMods",
                column: "RarityId",
                principalTable: "Rarities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ItemMods_Rarities_RarityId",
                table: "ItemMods");

            migrationBuilder.DropIndex(
                name: "IX_ItemMods_RarityId",
                table: "ItemMods");

            migrationBuilder.DropColumn(
                name: "RarityId",
                table: "ItemMods");
        }
    }
}
