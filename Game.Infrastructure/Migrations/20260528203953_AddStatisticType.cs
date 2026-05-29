using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStatisticType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StatisticTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatisticTypes", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "StatisticTypes",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Enemies Killed" },
                    { 2, "Bosses Defeated" },
                    { 3, "Zones Cleared" },
                    { 4, "Total Damage Dealt" },
                    { 5, "Highest Single Attack Damage" },
                    { 6, "Total Damage Taken" },
                    { 7, "Total Damage Healed" },
                    { 8, "Enemies Encountered" },
                    { 9, "Battles Won" },
                    { 10, "Battles Lost" },
                    { 11, "Player Deaths" },
                    { 12, "Total Battle Time Ms" },
                    { 13, "Fastest Victory Ms" },
                    { 14, "Total Skills Used" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerStatistics_StatisticTypeId",
                table: "PlayerStatistics",
                column: "StatisticTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerStatistics_StatisticTypes_StatisticTypeId",
                table: "PlayerStatistics",
                column: "StatisticTypeId",
                principalTable: "StatisticTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlayerStatistics_StatisticTypes_StatisticTypeId",
                table: "PlayerStatistics");

            migrationBuilder.DropTable(
                name: "StatisticTypes");

            migrationBuilder.DropIndex(
                name: "IX_PlayerStatistics_StatisticTypeId",
                table: "PlayerStatistics");
        }
    }
}
