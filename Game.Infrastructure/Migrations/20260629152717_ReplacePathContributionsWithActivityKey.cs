using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplacePathContributionsWithActivityKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SkillPathContributions");

            migrationBuilder.DropColumn(
                name: "FalloffBase",
                table: "Paths");

            migrationBuilder.AddColumn<int>(
                name: "ActivityKey",
                table: "Paths",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActivityKey",
                table: "Paths");

            migrationBuilder.AddColumn<decimal>(
                name: "FalloffBase",
                table: "Paths",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

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
                name: "IX_SkillPathContributions_PathId",
                table: "SkillPathContributions",
                column: "PathId");
        }
    }
}
