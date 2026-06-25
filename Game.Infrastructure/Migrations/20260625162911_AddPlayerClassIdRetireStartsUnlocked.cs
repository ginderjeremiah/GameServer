using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerClassIdRetireStartsUnlocked : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartsUnlocked",
                table: "Proficiencies");

            // Existing-character reset (spike #1126): every character now requires a class, and the game is
            // pre-release, so existing characters are deleted rather than grandfathered. Truncating Players
            // CASCADEs to every player-owned table (attributes, skills, inventory, progress, log preferences),
            // leaving the table empty so the new non-nullable ClassId has no rows to back-fill. Accounts (Users)
            // are kept — they simply have no characters until one is created with a class.
            migrationBuilder.Sql("""TRUNCATE TABLE "Players" RESTART IDENTITY CASCADE;""");

            migrationBuilder.AddColumn<int>(
                name: "ClassId",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClassId",
                table: "Players");

            migrationBuilder.AddColumn<bool>(
                name: "StartsUnlocked",
                table: "Proficiencies",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
