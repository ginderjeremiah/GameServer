using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StatisticsAndChallengesUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TargetCount",
                table: "Challenges",
                newName: "StatisticTypeId");

            migrationBuilder.AddColumn<int>(
                name: "EntityType",
                table: "StatisticTypes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "Value",
                table: "PlayerStatistics",
                type: "numeric(36,3)",
                precision: 36,
                scale: 3,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<decimal>(
                name: "Progress",
                table: "PlayerChallenges",
                type: "numeric(36,3)",
                precision: 36,
                scale: 3,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<decimal>(
                name: "ProgressGoal",
                table: "Challenges",
                type: "numeric(36,3)",
                precision: 36,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 1,
                column: "EntityType",
                value: 1);

            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 2,
                column: "EntityType",
                value: 0);

            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 3,
                column: "EntityType",
                value: 2);

            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 4,
                column: "EntityType",
                value: 0);

            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 5,
                column: "EntityType",
                value: 0);

            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 6,
                column: "EntityType",
                value: 0);

            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 7,
                column: "EntityType",
                value: 0);

            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 8,
                column: "EntityType",
                value: 1);

            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 9,
                column: "EntityType",
                value: 1);

            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 10,
                column: "EntityType",
                value: 1);

            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 11,
                column: "EntityType",
                value: 0);

            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 12,
                column: "EntityType",
                value: 0);

            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 13,
                column: "EntityType",
                value: 0);

            migrationBuilder.UpdateData(
                table: "StatisticTypes",
                keyColumn: "Id",
                keyValue: 14,
                column: "EntityType",
                value: 3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EntityType",
                table: "StatisticTypes");

            migrationBuilder.DropColumn(
                name: "ProgressGoal",
                table: "Challenges");

            migrationBuilder.RenameColumn(
                name: "StatisticTypeId",
                table: "Challenges",
                newName: "TargetCount");

            migrationBuilder.AlterColumn<long>(
                name: "Value",
                table: "PlayerStatistics",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,3)",
                oldPrecision: 36,
                oldScale: 3);

            migrationBuilder.AlterColumn<int>(
                name: "Progress",
                table: "PlayerChallenges",
                type: "integer",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,3)",
                oldPrecision: 36,
                oldScale: 3);
        }
    }
}
