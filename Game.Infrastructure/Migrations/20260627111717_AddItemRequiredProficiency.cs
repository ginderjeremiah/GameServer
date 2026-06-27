using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddItemRequiredProficiency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RequiredProficiencyId",
                table: "Items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequiredProficiencyLevel",
                table: "Items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Items_RequiredProficiencyId",
                table: "Items",
                column: "RequiredProficiencyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Proficiencies_RequiredProficiencyId",
                table: "Items",
                column: "RequiredProficiencyId",
                principalTable: "Proficiencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Items_Proficiencies_RequiredProficiencyId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_RequiredProficiencyId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "RequiredProficiencyId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "RequiredProficiencyLevel",
                table: "Items");
        }
    }
}
