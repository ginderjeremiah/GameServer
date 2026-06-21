using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillAcquisition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing skills predate the acquisition classification, so they backfill to Player
            // (ESkillAcquisition.Player == 1) — the prevailing channel. The model carries no default, so EF
            // always writes the explicit value on insert; this default only backfills the existing rows.
            migrationBuilder.AddColumn<int>(
                name: "Acquisition",
                table: "Skills",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Acquisition",
                table: "Skills");
        }
    }
}
