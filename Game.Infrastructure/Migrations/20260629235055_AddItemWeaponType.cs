using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddItemWeaponType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WeaponType",
                table: "Items",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WeaponType",
                table: "Items");
        }
    }
}
