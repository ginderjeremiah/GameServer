using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddItemGrantedSkill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GrantedSkillId",
                table: "Items",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Items_GrantedSkillId",
                table: "Items",
                column: "GrantedSkillId");

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Skills_GrantedSkillId",
                table: "Items",
                column: "GrantedSkillId",
                principalTable: "Skills",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Items_Skills_GrantedSkillId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_GrantedSkillId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "GrantedSkillId",
                table: "Items");
        }
    }
}
