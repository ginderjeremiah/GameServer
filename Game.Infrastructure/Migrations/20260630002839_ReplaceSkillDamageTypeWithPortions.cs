using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceSkillDamageTypeWithPortions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SkillDamagePortions",
                columns: table => new
                {
                    SkillId = table.Column<int>(type: "integer", nullable: false),
                    DamageType = table.Column<int>(type: "integer", nullable: false),
                    Weight = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillDamagePortions", x => new { x.SkillId, x.DamageType });
                    table.ForeignKey(
                        name: "FK_SkillDamagePortions_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Backfill every existing skill to a single full-weight Physical portion (pre-release; no
            // grandfathering of the dropped single DamageType column — spike #1343). Physical (0) carries no
            // amp/resist attributes, so battle behaviour is unchanged until typed content is authored.
            migrationBuilder.Sql(
                """
                INSERT INTO "SkillDamagePortions" ("SkillId", "DamageType", "Weight")
                SELECT "Id", 0, 1.0 FROM "Skills";
                """);

            migrationBuilder.DropColumn(
                name: "DamageType",
                table: "Skills");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DamageType",
                table: "Skills",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Best-effort restore: collapse each skill's portions back to its highest-weight (primary) type.
            migrationBuilder.Sql(
                """
                UPDATE "Skills" s
                SET "DamageType" = p."DamageType"
                FROM (
                    SELECT DISTINCT ON ("SkillId") "SkillId", "DamageType"
                    FROM "SkillDamagePortions"
                    ORDER BY "SkillId", "Weight" DESC, "DamageType"
                ) p
                WHERE p."SkillId" = s."Id";
                """);

            migrationBuilder.DropTable(
                name: "SkillDamagePortions");
        }
    }
}
