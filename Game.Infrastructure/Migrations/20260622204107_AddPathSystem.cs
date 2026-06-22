using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPathSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SkillProficiencies");

            migrationBuilder.AddColumn<int>(
                name: "PathId",
                table: "Proficiencies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PathOrdinal",
                table: "Proficiencies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Paths",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'0', '1', '0', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FalloffBase = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    RetiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Paths", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SkillPathContributions",
                columns: table => new
                {
                    SkillId = table.Column<int>(type: "integer", nullable: false),
                    PathId = table.Column<int>(type: "integer", nullable: false),
                    HomeTier = table.Column<int>(type: "integer", nullable: false),
                    Weight = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillPathContributions", x => new { x.SkillId, x.PathId });
                    table.ForeignKey(
                        name: "FK_SkillPathContributions_Paths_PathId",
                        column: x => x.PathId,
                        principalTable: "Paths",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SkillPathContributions_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Proficiencies_PathId_PathOrdinal",
                table: "Proficiencies",
                columns: new[] { "PathId", "PathOrdinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SkillPathContributions_PathId",
                table: "SkillPathContributions",
                column: "PathId");

            migrationBuilder.AddForeignKey(
                name: "FK_Proficiencies_Paths_PathId",
                table: "Proficiencies",
                column: "PathId",
                principalTable: "Paths",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Proficiencies_Paths_PathId",
                table: "Proficiencies");

            migrationBuilder.DropTable(
                name: "SkillPathContributions");

            migrationBuilder.DropTable(
                name: "Paths");

            migrationBuilder.DropIndex(
                name: "IX_Proficiencies_PathId_PathOrdinal",
                table: "Proficiencies");

            migrationBuilder.DropColumn(
                name: "PathId",
                table: "Proficiencies");

            migrationBuilder.DropColumn(
                name: "PathOrdinal",
                table: "Proficiencies");

            migrationBuilder.CreateTable(
                name: "SkillProficiencies",
                columns: table => new
                {
                    SkillId = table.Column<int>(type: "integer", nullable: false),
                    ProficiencyId = table.Column<int>(type: "integer", nullable: false),
                    Weight = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillProficiencies", x => new { x.SkillId, x.ProficiencyId });
                    table.ForeignKey(
                        name: "FK_SkillProficiencies_Proficiencies_ProficiencyId",
                        column: x => x.ProficiencyId,
                        principalTable: "Proficiencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SkillProficiencies_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SkillProficiencies_ProficiencyId",
                table: "SkillProficiencies",
                column: "ProficiencyId");
        }
    }
}
