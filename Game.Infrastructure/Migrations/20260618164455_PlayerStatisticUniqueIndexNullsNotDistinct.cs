using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PlayerStatisticUniqueIndexNullsNotDistinct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlayerStatistics_PlayerId_StatisticTypeId_EntityId",
                table: "PlayerStatistics");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerStatistics_PlayerId_StatisticTypeId_EntityId",
                table: "PlayerStatistics",
                columns: new[] { "PlayerId", "StatisticTypeId", "EntityId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlayerStatistics_PlayerId_StatisticTypeId_EntityId",
                table: "PlayerStatistics");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerStatistics_PlayerId_StatisticTypeId_EntityId",
                table: "PlayerStatistics",
                columns: new[] { "PlayerId", "StatisticTypeId", "EntityId" },
                unique: true);
        }
    }
}
