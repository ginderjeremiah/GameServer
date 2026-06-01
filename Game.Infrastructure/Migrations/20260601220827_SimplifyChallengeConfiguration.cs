using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyChallengeConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Challenges_StatisticTypes_StatisticTypeId",
                table: "Challenges");

            migrationBuilder.DropIndex(
                name: "IX_Challenges_StatisticTypeId",
                table: "Challenges");

            migrationBuilder.DropColumn(
                name: "EntityTypeId",
                table: "Challenges");

            migrationBuilder.DropColumn(
                name: "StatisticTypeId",
                table: "Challenges");

            migrationBuilder.CreateTable(
                name: "ChallengeTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StatisticTypeId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChallengeTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChallengeTypes_StatisticTypes_StatisticTypeId",
                        column: x => x.StatisticTypeId,
                        principalTable: "StatisticTypes",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "ChallengeTypes",
                columns: new[] { "Id", "Name", "StatisticTypeId" },
                values: new object[,]
                {
                    { 1, "Enemies Killed", 1 },
                    { 2, "Bosses Defeated", 2 },
                    { 3, "Zones Cleared", 3 },
                    { 4, "Time Trial", 13 },
                    { 5, "Level Reached", null },
                    { 6, "Damage Dealt", 4 },
                    { 7, "Battles Won", 9 },
                    { 8, "Skills Used", 14 }
                });

            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 4,
                column: "Name",
                value: "Damage Dealt");

            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 6,
                column: "Name",
                value: "Damage Taken");

            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 7,
                column: "Name",
                value: "Damage Healed");

            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 12,
                column: "Name",
                value: "Total Battle Time");

            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 13,
                column: "Name",
                value: "Fastest Victory");

            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 14,
                column: "Name",
                value: "Skills Used");

            migrationBuilder.CreateIndex(
                name: "IX_Challenges_ChallengeTypeId",
                table: "Challenges",
                column: "ChallengeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeTypes_StatisticTypeId",
                table: "ChallengeTypes",
                column: "StatisticTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Challenges_ChallengeTypes_ChallengeTypeId",
                table: "Challenges",
                column: "ChallengeTypeId",
                principalTable: "ChallengeTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Challenges_ChallengeTypes_ChallengeTypeId",
                table: "Challenges");

            migrationBuilder.DropTable(
                name: "ChallengeTypes");

            migrationBuilder.DropIndex(
                name: "IX_Challenges_ChallengeTypeId",
                table: "Challenges");

            migrationBuilder.AddColumn<int>(
                name: "EntityTypeId",
                table: "Challenges",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StatisticTypeId",
                table: "Challenges",
                type: "integer",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 4,
                column: "Name",
                value: "Total Damage Dealt");

            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 6,
                column: "Name",
                value: "Total Damage Taken");

            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 7,
                column: "Name",
                value: "Total Damage Healed");

            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 12,
                column: "Name",
                value: "Total Battle Time Ms");

            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 13,
                column: "Name",
                value: "Fastest Victory Ms");

            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 14,
                column: "Name",
                value: "Total Skills Used");

            migrationBuilder.CreateIndex(
                name: "IX_Challenges_StatisticTypeId",
                table: "Challenges",
                column: "StatisticTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Challenges_StatisticTypes_StatisticTypeId",
                table: "Challenges",
                column: "StatisticTypeId",
                principalTable: "StatisticTypes",
                principalColumn: "Id");
        }
    }
}
