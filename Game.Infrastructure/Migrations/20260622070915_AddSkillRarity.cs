using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillRarity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing skills predate the rarity tier, so they backfill to Common (ERarity.Common == 1, the
            // Rarities seed's first id). The model carries no default, so EF always writes the explicit
            // authored value on insert; this default only backfills the existing rows.
            migrationBuilder.AddColumn<int>(
                name: "RarityId",
                table: "Skills",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_Skills_RarityId",
                table: "Skills",
                column: "RarityId");

            migrationBuilder.AddForeignKey(
                name: "FK_Skills_Rarities_RarityId",
                table: "Skills",
                column: "RarityId",
                principalTable: "Rarities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Skills_Rarities_RarityId",
                table: "Skills");

            migrationBuilder.DropIndex(
                name: "IX_Skills_RarityId",
                table: "Skills");

            migrationBuilder.DropColumn(
                name: "RarityId",
                table: "Skills");
        }
    }
}
