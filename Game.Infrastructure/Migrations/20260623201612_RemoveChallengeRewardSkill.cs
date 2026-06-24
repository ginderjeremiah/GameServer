using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveChallengeRewardSkill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Challenges_Skills_RewardSkillId",
                table: "Challenges");

            migrationBuilder.DropIndex(
                name: "IX_Challenges_RewardSkillId",
                table: "Challenges");

            migrationBuilder.DropColumn(
                name: "RewardSkillId",
                table: "Challenges");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RewardSkillId",
                table: "Challenges",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Challenges_RewardSkillId",
                table: "Challenges",
                column: "RewardSkillId");

            migrationBuilder.AddForeignKey(
                name: "FK_Challenges_Skills_RewardSkillId",
                table: "Challenges",
                column: "RewardSkillId",
                principalTable: "Skills",
                principalColumn: "Id");
        }
    }
}
