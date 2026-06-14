using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExplicitTagJoinEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ItemModTags_ItemMods_ItemModsId",
                table: "ItemModTags");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemModTags_Tags_TagsId",
                table: "ItemModTags");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemTags_Items_ItemsId",
                table: "ItemTags");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemTags_Tags_TagsId",
                table: "ItemTags");

            migrationBuilder.RenameColumn(
                name: "TagsId",
                table: "ItemTags",
                newName: "TagId");

            migrationBuilder.RenameColumn(
                name: "ItemsId",
                table: "ItemTags",
                newName: "ItemId");

            migrationBuilder.RenameIndex(
                name: "IX_ItemTags_TagsId",
                table: "ItemTags",
                newName: "IX_ItemTags_TagId");

            migrationBuilder.RenameColumn(
                name: "TagsId",
                table: "ItemModTags",
                newName: "TagId");

            migrationBuilder.RenameColumn(
                name: "ItemModsId",
                table: "ItemModTags",
                newName: "ItemModId");

            migrationBuilder.RenameIndex(
                name: "IX_ItemModTags_TagsId",
                table: "ItemModTags",
                newName: "IX_ItemModTags_TagId");

            migrationBuilder.AddForeignKey(
                name: "FK_ItemModTags_ItemMods_ItemModId",
                table: "ItemModTags",
                column: "ItemModId",
                principalTable: "ItemMods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemModTags_Tags_TagId",
                table: "ItemModTags",
                column: "TagId",
                principalTable: "Tags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemTags_Items_ItemId",
                table: "ItemTags",
                column: "ItemId",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemTags_Tags_TagId",
                table: "ItemTags",
                column: "TagId",
                principalTable: "Tags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ItemModTags_ItemMods_ItemModId",
                table: "ItemModTags");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemModTags_Tags_TagId",
                table: "ItemModTags");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemTags_Items_ItemId",
                table: "ItemTags");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemTags_Tags_TagId",
                table: "ItemTags");

            migrationBuilder.RenameColumn(
                name: "TagId",
                table: "ItemTags",
                newName: "TagsId");

            migrationBuilder.RenameColumn(
                name: "ItemId",
                table: "ItemTags",
                newName: "ItemsId");

            migrationBuilder.RenameIndex(
                name: "IX_ItemTags_TagId",
                table: "ItemTags",
                newName: "IX_ItemTags_TagsId");

            migrationBuilder.RenameColumn(
                name: "TagId",
                table: "ItemModTags",
                newName: "TagsId");

            migrationBuilder.RenameColumn(
                name: "ItemModId",
                table: "ItemModTags",
                newName: "ItemModsId");

            migrationBuilder.RenameIndex(
                name: "IX_ItemModTags_TagId",
                table: "ItemModTags",
                newName: "IX_ItemModTags_TagsId");

            migrationBuilder.AddForeignKey(
                name: "FK_ItemModTags_ItemMods_ItemModsId",
                table: "ItemModTags",
                column: "ItemModsId",
                principalTable: "ItemMods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemModTags_Tags_TagsId",
                table: "ItemModTags",
                column: "TagsId",
                principalTable: "Tags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemTags_Items_ItemsId",
                table: "ItemTags",
                column: "ItemsId",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemTags_Tags_TagsId",
                table: "ItemTags",
                column: "TagsId",
                principalTable: "Tags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
