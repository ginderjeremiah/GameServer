using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserLoginTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BrowserInfos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    SecChUa = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SecChUaMobile = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SecChUaPlatform = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrowserInfos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceFingerprintHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    BrowserInfoId = table.Column<int>(type: "integer", nullable: false),
                    DeviceMemory = table.Column<double>(type: "double precision", nullable: true),
                    HardwareConcurrency = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Devices_BrowserInfos_BrowserInfoId",
                        column: x => x.BrowserInfoId,
                        principalTable: "BrowserInfos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLogins",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    DeviceId = table.Column<int>(type: "integer", nullable: false),
                    LastConnection = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLogins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserLogins_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserLogins_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BrowserInfos_UserAgent",
                table: "BrowserInfos",
                column: "UserAgent",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_BrowserInfoId",
                table: "Devices",
                column: "BrowserInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_DeviceFingerprintHash",
                table: "Devices",
                column: "DeviceFingerprintHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLogins_DeviceId",
                table: "UserLogins",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLogins_UserId_IpAddress_DeviceId",
                table: "UserLogins",
                columns: new[] { "UserId", "IpAddress", "DeviceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserLogins");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "BrowserInfos");
        }
    }
}
