using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProficiencySeedSkill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Proficiencies_Skills_SeedSkillId",
                table: "Proficiencies");

            migrationBuilder.DropIndex(
                name: "IX_Proficiencies_SeedSkillId",
                table: "Proficiencies");

            migrationBuilder.DropColumn(
                name: "SeedSkillId",
                table: "Proficiencies");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SeedSkillId",
                table: "Proficiencies",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Proficiencies_SeedSkillId",
                table: "Proficiencies",
                column: "SeedSkillId");

            migrationBuilder.AddForeignKey(
                name: "FK_Proficiencies_Skills_SeedSkillId",
                table: "Proficiencies",
                column: "SeedSkillId",
                principalTable: "Skills",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
