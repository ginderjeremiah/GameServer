using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedChallengeData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // KillCount challenges (ChallengeTypeId = 1)
            migrationBuilder.InsertData(
                table: "Challenges",
                columns: ["Name", "Description", "ChallengeTypeId", "TargetEntityId", "TargetCount", "RewardItemId", "RewardItemModId"],
                values: new object[] { "First Blood", "Defeat your first enemy.", 1, null, 1, null, null });

            migrationBuilder.InsertData(
                table: "Challenges",
                columns: ["Name", "Description", "ChallengeTypeId", "TargetEntityId", "TargetCount", "RewardItemId", "RewardItemModId"],
                values: new object[] { "Novice Slayer", "Defeat 10 enemies.", 1, null, 10, null, null });

            migrationBuilder.InsertData(
                table: "Challenges",
                columns: ["Name", "Description", "ChallengeTypeId", "TargetEntityId", "TargetCount", "RewardItemId", "RewardItemModId"],
                values: new object[] { "Seasoned Hunter", "Defeat 50 enemies.", 1, null, 50, null, null });

            migrationBuilder.InsertData(
                table: "Challenges",
                columns: ["Name", "Description", "ChallengeTypeId", "TargetEntityId", "TargetCount", "RewardItemId", "RewardItemModId"],
                values: new object[] { "Veteran Warrior", "Defeat 100 enemies.", 1, null, 100, null, null });

            migrationBuilder.InsertData(
                table: "Challenges",
                columns: ["Name", "Description", "ChallengeTypeId", "TargetEntityId", "TargetCount", "RewardItemId", "RewardItemModId"],
                values: new object[] { "Elite Exterminator", "Defeat 500 enemies.", 1, null, 500, null, null });

            // DamageDealt challenges (ChallengeTypeId = 6)
            migrationBuilder.InsertData(
                table: "Challenges",
                columns: ["Name", "Description", "ChallengeTypeId", "TargetEntityId", "TargetCount", "RewardItemId", "RewardItemModId"],
                values: new object[] { "First Strike", "Deal 100 total damage.", 6, null, 100, null, null });

            migrationBuilder.InsertData(
                table: "Challenges",
                columns: ["Name", "Description", "ChallengeTypeId", "TargetEntityId", "TargetCount", "RewardItemId", "RewardItemModId"],
                values: new object[] { "Damage Dealer", "Deal 10,000 total damage.", 6, null, 10000, null, null });

            migrationBuilder.InsertData(
                table: "Challenges",
                columns: ["Name", "Description", "ChallengeTypeId", "TargetEntityId", "TargetCount", "RewardItemId", "RewardItemModId"],
                values: new object[] { "Destructive Force", "Deal 100,000 total damage.", 6, null, 100000, null, null });

            // BattlesWon challenges (ChallengeTypeId = 7)
            migrationBuilder.InsertData(
                table: "Challenges",
                columns: ["Name", "Description", "ChallengeTypeId", "TargetEntityId", "TargetCount", "RewardItemId", "RewardItemModId"],
                values: new object[] { "Victor", "Win your first battle.", 7, null, 1, null, null });

            migrationBuilder.InsertData(
                table: "Challenges",
                columns: ["Name", "Description", "ChallengeTypeId", "TargetEntityId", "TargetCount", "RewardItemId", "RewardItemModId"],
                values: new object[] { "Triumphant", "Win 25 battles.", 7, null, 25, null, null });

            migrationBuilder.InsertData(
                table: "Challenges",
                columns: ["Name", "Description", "ChallengeTypeId", "TargetEntityId", "TargetCount", "RewardItemId", "RewardItemModId"],
                values: new object[] { "Undefeatable", "Win 100 battles.", 7, null, 100, null, null });

            // SkillsUsed challenges (ChallengeTypeId = 8)
            migrationBuilder.InsertData(
                table: "Challenges",
                columns: ["Name", "Description", "ChallengeTypeId", "TargetEntityId", "TargetCount", "RewardItemId", "RewardItemModId"],
                values: new object[] { "Skill Novice", "Use 10 skills in battle.", 8, null, 10, null, null });

            migrationBuilder.InsertData(
                table: "Challenges",
                columns: ["Name", "Description", "ChallengeTypeId", "TargetEntityId", "TargetCount", "RewardItemId", "RewardItemModId"],
                values: new object[] { "Skill Adept", "Use 100 skills in battle.", 8, null, 100, null, null });

            migrationBuilder.InsertData(
                table: "Challenges",
                columns: ["Name", "Description", "ChallengeTypeId", "TargetEntityId", "TargetCount", "RewardItemId", "RewardItemModId"],
                values: new object[] { "Skill Master", "Use 500 skills in battle.", 8, null, 500, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM \"Challenges\"");
        }
    }
}
