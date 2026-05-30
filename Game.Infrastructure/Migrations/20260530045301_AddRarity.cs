using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRarity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RarityId",
                table: "Items",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "Rarities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rarities", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Rarities",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Common" },
                    { 2, "Uncommon" },
                    { 3, "Rare" },
                    { 4, "Epic" },
                    { 5, "Legendary" },
                    { 6, "Mythic" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Items_RarityId",
                table: "Items",
                column: "RarityId");

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Rarities_RarityId",
                table: "Items",
                column: "RarityId",
                principalTable: "Rarities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Items_Rarities_RarityId",
                table: "Items");

            migrationBuilder.DropTable(
                name: "Rarities");

            migrationBuilder.DropIndex(
                name: "IX_Items_RarityId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "RarityId",
                table: "Items");
        }
    }
}
