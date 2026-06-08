using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddZoneUnlockChallenge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UnlockChallengeId",
                table: "Zones",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Zones_UnlockChallengeId",
                table: "Zones",
                column: "UnlockChallengeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Zones_Challenges_UnlockChallengeId",
                table: "Zones",
                column: "UnlockChallengeId",
                principalTable: "Challenges",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Zones_Challenges_UnlockChallengeId",
                table: "Zones");

            migrationBuilder.DropIndex(
                name: "IX_Zones_UnlockChallengeId",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "UnlockChallengeId",
                table: "Zones");
        }
    }
}
