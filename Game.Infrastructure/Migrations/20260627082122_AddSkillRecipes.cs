using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillRecipes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SkillRecipes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'0', '1', '0', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ResultSkillId = table.Column<int>(type: "integer", nullable: false),
                    RetiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillRecipes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SkillRecipes_Skills_ResultSkillId",
                        column: x => x.ResultSkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SkillRecipeConditions",
                columns: table => new
                {
                    RecipeId = table.Column<int>(type: "integer", nullable: false),
                    ProficiencyId = table.Column<int>(type: "integer", nullable: false),
                    MinLevel = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillRecipeConditions", x => new { x.RecipeId, x.ProficiencyId });
                    table.ForeignKey(
                        name: "FK_SkillRecipeConditions_Proficiencies_ProficiencyId",
                        column: x => x.ProficiencyId,
                        principalTable: "Proficiencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SkillRecipeConditions_SkillRecipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "SkillRecipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SkillRecipeInputs",
                columns: table => new
                {
                    RecipeId = table.Column<int>(type: "integer", nullable: false),
                    SkillId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillRecipeInputs", x => new { x.RecipeId, x.SkillId });
                    table.ForeignKey(
                        name: "FK_SkillRecipeInputs_SkillRecipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "SkillRecipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SkillRecipeInputs_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SkillRecipeConditions_ProficiencyId",
                table: "SkillRecipeConditions",
                column: "ProficiencyId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillRecipeInputs_SkillId",
                table: "SkillRecipeInputs",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillRecipes_ResultSkillId",
                table: "SkillRecipes",
                column: "ResultSkillId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SkillRecipeConditions");

            migrationBuilder.DropTable(
                name: "SkillRecipeInputs");

            migrationBuilder.DropTable(
                name: "SkillRecipes");
        }
    }
}
