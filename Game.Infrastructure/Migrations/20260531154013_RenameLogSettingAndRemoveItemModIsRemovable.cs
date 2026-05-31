using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameLogSettingAndRemoveItemModIsRemovable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LogPreferences_LogSettings_LogSettingId",
                table: "LogPreferences");

            migrationBuilder.DropTable(
                name: "LogSettings");

            migrationBuilder.DropColumn(
                name: "Removable",
                table: "ItemMods");

            migrationBuilder.RenameColumn(
                name: "LogSettingId",
                table: "LogPreferences",
                newName: "LogTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_LogPreferences_LogSettingId",
                table: "LogPreferences",
                newName: "IX_LogPreferences_LogTypeId");

            migrationBuilder.CreateTable(
                name: "LogTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DefaultValue = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogTypes", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "LogTypes",
                columns: new[] { "Id", "DefaultValue", "Name" },
                values: new object[,]
                {
                    { 1, false, "Damage" },
                    { 2, false, "Debug" },
                    { 3, false, "Exp" },
                    { 4, false, "Level Up" },
                    { 5, false, "Item Found" },
                    { 6, false, "Enemy Defeated" }
                });

            migrationBuilder.AddForeignKey(
                name: "FK_LogPreferences_LogTypes_LogTypeId",
                table: "LogPreferences",
                column: "LogTypeId",
                principalTable: "LogTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LogPreferences_LogTypes_LogTypeId",
                table: "LogPreferences");

            migrationBuilder.DropTable(
                name: "LogTypes");

            migrationBuilder.RenameColumn(
                name: "LogTypeId",
                table: "LogPreferences",
                newName: "LogSettingId");

            migrationBuilder.RenameIndex(
                name: "IX_LogPreferences_LogTypeId",
                table: "LogPreferences",
                newName: "IX_LogPreferences_LogSettingId");

            migrationBuilder.AddColumn<bool>(
                name: "Removable",
                table: "ItemMods",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "LogSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    DefaultValue = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "LogSettings",
                columns: new[] { "Id", "DefaultValue", "Name" },
                values: new object[,]
                {
                    { 1, false, "Damage" },
                    { 2, false, "Debug" },
                    { 3, false, "Exp" },
                    { 4, false, "Level Up" },
                    { 5, false, "Item Found" },
                    { 6, false, "Enemy Defeated" }
                });

            migrationBuilder.AddForeignKey(
                name: "FK_LogPreferences_LogSettings_LogSettingId",
                table: "LogPreferences",
                column: "LogSettingId",
                principalTable: "LogSettings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
