using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEntityTypeToChallenges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "StatisticTypeId",
                table: "Challenges",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "EntityTypeId",
                table: "Challenges",
                type: "integer",
                nullable: false,
                defaultValue: 0);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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

            migrationBuilder.AlterColumn<int>(
                name: "StatisticTypeId",
                table: "Challenges",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
