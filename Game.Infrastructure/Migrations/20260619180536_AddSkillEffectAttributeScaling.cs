using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillEffectAttributeScaling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ScalingAmount",
                table: "SkillEffects",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ScalingAttributeId",
                table: "SkillEffects",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_SkillEffects_ScalingAttributeId",
                table: "SkillEffects",
                column: "ScalingAttributeId");

            migrationBuilder.AddForeignKey(
                name: "FK_SkillEffects_Attributes_ScalingAttributeId",
                table: "SkillEffects",
                column: "ScalingAttributeId",
                principalTable: "Attributes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SkillEffects_Attributes_ScalingAttributeId",
                table: "SkillEffects");

            migrationBuilder.DropIndex(
                name: "IX_SkillEffects_ScalingAttributeId",
                table: "SkillEffects");

            migrationBuilder.DropColumn(
                name: "ScalingAmount",
                table: "SkillEffects");

            migrationBuilder.DropColumn(
                name: "ScalingAttributeId",
                table: "SkillEffects");
        }
    }
}
