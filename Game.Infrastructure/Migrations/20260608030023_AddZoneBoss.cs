using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddZoneBoss : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BossEnemyId",
                table: "Zones",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BossLevel",
                table: "Zones",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_Zones_BossEnemyId",
                table: "Zones",
                column: "BossEnemyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Zones_Enemies_BossEnemyId",
                table: "Zones",
                column: "BossEnemyId",
                principalTable: "Enemies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Zones_Enemies_BossEnemyId",
                table: "Zones");

            migrationBuilder.DropIndex(
                name: "IX_Zones_BossEnemyId",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "BossEnemyId",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "BossLevel",
                table: "Zones");
        }
    }
}
