using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveZoneEnemyId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ZoneEnemies",
                table: "ZoneEnemies");

            migrationBuilder.DropIndex(
                name: "IX_ZoneEnemies_ZoneId",
                table: "ZoneEnemies");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "ZoneEnemies");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ZoneEnemies",
                table: "ZoneEnemies",
                columns: new[] { "ZoneId", "EnemyId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ZoneEnemies",
                table: "ZoneEnemies");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "ZoneEnemies",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ZoneEnemies",
                table: "ZoneEnemies",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_ZoneEnemies_ZoneId",
                table: "ZoneEnemies",
                column: "ZoneId");
        }
    }
}
