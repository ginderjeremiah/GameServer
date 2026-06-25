using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClassSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Classes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'0', '1', '0', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Word = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PassiveAttributeId = table.Column<int>(type: "integer", nullable: false),
                    PassiveAmount = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    PassiveScalingAttributeId = table.Column<int>(type: "integer", nullable: true),
                    PassiveScalingAmount = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    PassiveModifierType = table.Column<int>(type: "integer", nullable: false),
                    RetiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Classes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClassAttributeDistributions",
                columns: table => new
                {
                    ClassId = table.Column<int>(type: "integer", nullable: false),
                    AttributeId = table.Column<int>(type: "integer", nullable: false),
                    BaseAmount = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    AmountPerLevel = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassAttributeDistributions", x => new { x.ClassId, x.AttributeId });
                    table.ForeignKey(
                        name: "FK_ClassAttributeDistributions_Attributes_AttributeId",
                        column: x => x.AttributeId,
                        principalTable: "Attributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassAttributeDistributions_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClassStarterEquipment",
                columns: table => new
                {
                    ClassId = table.Column<int>(type: "integer", nullable: false),
                    EquipmentSlotId = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassStarterEquipment", x => new { x.ClassId, x.EquipmentSlotId });
                    table.ForeignKey(
                        name: "FK_ClassStarterEquipment_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassStarterEquipment_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClassStarterSkills",
                columns: table => new
                {
                    ClassId = table.Column<int>(type: "integer", nullable: false),
                    SkillId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassStarterSkills", x => new { x.ClassId, x.SkillId });
                    table.ForeignKey(
                        name: "FK_ClassStarterSkills_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassStarterSkills_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClassAttributeDistributions_AttributeId",
                table: "ClassAttributeDistributions",
                column: "AttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassStarterEquipment_ItemId",
                table: "ClassStarterEquipment",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassStarterSkills_SkillId",
                table: "ClassStarterSkills",
                column: "SkillId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClassAttributeDistributions");

            migrationBuilder.DropTable(
                name: "ClassStarterEquipment");

            migrationBuilder.DropTable(
                name: "ClassStarterSkills");

            migrationBuilder.DropTable(
                name: "Classes");
        }
    }
}
