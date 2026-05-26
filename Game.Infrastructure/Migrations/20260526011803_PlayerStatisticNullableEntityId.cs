using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PlayerStatisticNullableEntityId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_PlayerStatistics",
                table: "PlayerStatistics");

            migrationBuilder.AlterColumn<int>(
                name: "EntityId",
                table: "PlayerStatistics",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "PlayerStatistics",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlayerStatistics",
                table: "PlayerStatistics",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerStatistics_PlayerId_StatisticTypeId_EntityId",
                table: "PlayerStatistics",
                columns: new[] { "PlayerId", "StatisticTypeId", "EntityId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_PlayerStatistics",
                table: "PlayerStatistics");

            migrationBuilder.DropIndex(
                name: "IX_PlayerStatistics_PlayerId_StatisticTypeId_EntityId",
                table: "PlayerStatistics");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "PlayerStatistics");

            migrationBuilder.AlterColumn<int>(
                name: "EntityId",
                table: "PlayerStatistics",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlayerStatistics",
                table: "PlayerStatistics",
                columns: new[] { "PlayerId", "StatisticTypeId", "EntityId" });
        }
    }
}
