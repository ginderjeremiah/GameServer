using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDesignerNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DesignerNotes",
                table: "Zones",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DesignerNotes",
                table: "Skills",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DesignerNotes",
                table: "SkillRecipes",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DesignerNotes",
                table: "Proficiencies",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DesignerNotes",
                table: "Paths",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DesignerNotes",
                table: "Items",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DesignerNotes",
                table: "ItemMods",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DesignerNotes",
                table: "Enemies",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DesignerNotes",
                table: "Classes",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DesignerNotes",
                table: "Challenges",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DesignerNotes",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "DesignerNotes",
                table: "Skills");

            migrationBuilder.DropColumn(
                name: "DesignerNotes",
                table: "SkillRecipes");

            migrationBuilder.DropColumn(
                name: "DesignerNotes",
                table: "Proficiencies");

            migrationBuilder.DropColumn(
                name: "DesignerNotes",
                table: "Paths");

            migrationBuilder.DropColumn(
                name: "DesignerNotes",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "DesignerNotes",
                table: "ItemMods");

            migrationBuilder.DropColumn(
                name: "DesignerNotes",
                table: "Enemies");

            migrationBuilder.DropColumn(
                name: "DesignerNotes",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "DesignerNotes",
                table: "Challenges");
        }
    }
}
