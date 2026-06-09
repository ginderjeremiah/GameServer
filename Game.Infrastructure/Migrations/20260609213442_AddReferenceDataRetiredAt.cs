using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReferenceDataRetiredAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RetiredAt",
                table: "Zones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetiredAt",
                table: "Skills",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetiredAt",
                table: "Items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetiredAt",
                table: "ItemMods",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetiredAt",
                table: "Enemies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetiredAt",
                table: "Challenges",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RetiredAt",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "RetiredAt",
                table: "Skills");

            migrationBuilder.DropColumn(
                name: "RetiredAt",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "RetiredAt",
                table: "ItemMods");

            migrationBuilder.DropColumn(
                name: "RetiredAt",
                table: "Enemies");

            migrationBuilder.DropColumn(
                name: "RetiredAt",
                table: "Challenges");
        }
    }
}
