using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProficiencySystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Proficiencies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'0', '1', '0', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IconPath = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MaxLevel = table.Column<int>(type: "integer", nullable: false),
                    BaseXp = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    XpGrowth = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    StartsUnlocked = table.Column<bool>(type: "boolean", nullable: false),
                    SeedSkillId = table.Column<int>(type: "integer", nullable: true),
                    RetiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proficiencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Proficiencies_Skills_SeedSkillId",
                        column: x => x.SeedSkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProficiencyLevelModifiers",
                columns: table => new
                {
                    ProficiencyId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    AttributeId = table.Column<int>(type: "integer", nullable: false),
                    ModifierType = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProficiencyLevelModifiers", x => new { x.ProficiencyId, x.Level, x.AttributeId });
                    table.ForeignKey(
                        name: "FK_ProficiencyLevelModifiers_Attributes_AttributeId",
                        column: x => x.AttributeId,
                        principalTable: "Attributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProficiencyLevelModifiers_Proficiencies_ProficiencyId",
                        column: x => x.ProficiencyId,
                        principalTable: "Proficiencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProficiencyLevelRewards",
                columns: table => new
                {
                    ProficiencyId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    RewardSkillId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProficiencyLevelRewards", x => new { x.ProficiencyId, x.Level });
                    table.ForeignKey(
                        name: "FK_ProficiencyLevelRewards_Proficiencies_ProficiencyId",
                        column: x => x.ProficiencyId,
                        principalTable: "Proficiencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProficiencyLevelRewards_Skills_RewardSkillId",
                        column: x => x.RewardSkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

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
                name: "IX_Proficiencies_SeedSkillId",
                table: "Proficiencies",
                column: "SeedSkillId");

            migrationBuilder.CreateIndex(
                name: "IX_ProficiencyLevelModifiers_AttributeId",
                table: "ProficiencyLevelModifiers",
                column: "AttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProficiencyLevelRewards_RewardSkillId",
                table: "ProficiencyLevelRewards",
                column: "RewardSkillId");

            migrationBuilder.CreateIndex(
                name: "IX_ProficiencyPrerequisites_PrerequisiteProficiencyId",
                table: "ProficiencyPrerequisites",
                column: "PrerequisiteProficiencyId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillProficiencies_ProficiencyId",
                table: "SkillProficiencies",
                column: "ProficiencyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProficiencyLevelModifiers");

            migrationBuilder.DropTable(
                name: "ProficiencyLevelRewards");

            migrationBuilder.DropTable(
                name: "ProficiencyPrerequisites");

            migrationBuilder.DropTable(
                name: "SkillProficiencies");

            migrationBuilder.DropTable(
                name: "Proficiencies");
        }
    }
}
