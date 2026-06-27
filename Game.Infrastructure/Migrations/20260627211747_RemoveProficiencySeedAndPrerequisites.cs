using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProficiencySeedAndPrerequisites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Proficiencies_Skills_SeedSkillId",
                table: "Proficiencies");

            migrationBuilder.DropTable(
                name: "ProficiencyPrerequisites");

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

            migrationBuilder.CreateTable(
                name: "ProficiencyPrerequisites",
                columns: table => new
                {
                    ProficiencyId = table.Column<int>(type: "integer", nullable: false),
                    PrerequisiteProficiencyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProficiencyPrerequisites", x => new { x.ProficiencyId, x.PrerequisiteProficiencyId });
                    table.ForeignKey(
                        name: "FK_ProficiencyPrerequisites_Proficiencies_PrerequisiteProficie~",
                        column: x => x.PrerequisiteProficiencyId,
                        principalTable: "Proficiencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProficiencyPrerequisites_Proficiencies_ProficiencyId",
                        column: x => x.ProficiencyId,
                        principalTable: "Proficiencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Proficiencies_SeedSkillId",
                table: "Proficiencies",
                column: "SeedSkillId");

            migrationBuilder.CreateIndex(
                name: "IX_ProficiencyPrerequisites_PrerequisiteProficiencyId",
                table: "ProficiencyPrerequisites",
                column: "PrerequisiteProficiencyId");

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
