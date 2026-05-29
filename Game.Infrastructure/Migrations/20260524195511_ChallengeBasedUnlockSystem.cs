using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChallengeBasedUnlockSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ItemModSlots_ItemMods_GuaranteedItemModId",
                table: "ItemModSlots");

            migrationBuilder.DropTable(
                name: "EnemyDrops");

            migrationBuilder.DropTable(
                name: "InventoryItemMods");

            migrationBuilder.DropTable(
                name: "ZoneDrops");

            migrationBuilder.DropTable(
                name: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_ItemModSlots_GuaranteedItemModId",
                table: "ItemModSlots");

            migrationBuilder.DropColumn(
                name: "PassHash",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Salt",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "UserName",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "GuaranteedItemModId",
                table: "ItemModSlots");

            migrationBuilder.DropColumn(
                name: "Probability",
                table: "ItemModSlots");

            migrationBuilder.AddColumn<int>(
                name: "CurrentZoneId",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActivity",
                table: "Players",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "IconPath",
                table: "Items",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<int>(
                name: "Index",
                table: "ItemModSlots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AppliedMods",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    ItemModSlotId = table.Column<int>(type: "integer", nullable: false),
                    ItemModId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppliedMods", x => new { x.PlayerId, x.ItemId, x.ItemModSlotId });
                    table.ForeignKey(
                        name: "FK_AppliedMods_ItemModSlots_ItemModSlotId",
                        column: x => x.ItemModSlotId,
                        principalTable: "ItemModSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppliedMods_ItemMods_ItemModId",
                        column: x => x.ItemModId,
                        principalTable: "ItemMods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppliedMods_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppliedMods_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Challenges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'0', '1', '0', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ChallengeTypeId = table.Column<int>(type: "integer", nullable: false),
                    TargetEntityId = table.Column<int>(type: "integer", nullable: true),
                    TargetCount = table.Column<int>(type: "integer", nullable: false),
                    RewardItemId = table.Column<int>(type: "integer", nullable: true),
                    RewardItemModId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Challenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Challenges_ItemMods_RewardItemModId",
                        column: x => x.RewardItemModId,
                        principalTable: "ItemMods",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Challenges_Items_RewardItemId",
                        column: x => x.RewardItemId,
                        principalTable: "Items",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlayerStatistics",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    StatisticTypeId = table.Column<int>(type: "integer", nullable: false),
                    EntityId = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerStatistics", x => new { x.PlayerId, x.StatisticTypeId, x.EntityId });
                    table.ForeignKey(
                        name: "FK_PlayerStatistics_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UnlockedItems",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    EquipmentSlotId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnlockedItems", x => new { x.PlayerId, x.ItemId });
                    table.ForeignKey(
                        name: "FK_UnlockedItems_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UnlockedItems_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UnlockedMods",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    ItemModId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnlockedMods", x => new { x.PlayerId, x.ItemModId });
                    table.ForeignKey(
                        name: "FK_UnlockedMods_ItemMods_ItemModId",
                        column: x => x.ItemModId,
                        principalTable: "ItemMods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UnlockedMods_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Salt = table.Column<Guid>(type: "uuid", nullable: false),
                    PassHash = table.Column<string>(type: "character varying(88)", maxLength: 88, nullable: false),
                    LastLogin = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlayerChallenges",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    ChallengeId = table.Column<int>(type: "integer", nullable: false),
                    Progress = table.Column<int>(type: "integer", nullable: false),
                    Completed = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerChallenges", x => new { x.PlayerId, x.ChallengeId });
                    table.ForeignKey(
                        name: "FK_PlayerChallenges_Challenges_ChallengeId",
                        column: x => x.ChallengeId,
                        principalTable: "Challenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerChallenges_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "LogSettings",
                keyColumn: "Id",
                keyValue: 3,
                column: "DefaultValue",
                value: false);

            migrationBuilder.UpdateData(
                table: "LogSettings",
                keyColumn: "Id",
                keyValue: 4,
                column: "DefaultValue",
                value: false);

            migrationBuilder.UpdateData(
                table: "LogSettings",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "DefaultValue", "Name" },
                values: new object[] { false, "Item Found" });

            migrationBuilder.UpdateData(
                table: "LogSettings",
                keyColumn: "Id",
                keyValue: 6,
                column: "DefaultValue",
                value: false);

            migrationBuilder.CreateIndex(
                name: "IX_Players_UserId",
                table: "Players",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppliedMods_ItemId",
                table: "AppliedMods",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AppliedMods_ItemModId",
                table: "AppliedMods",
                column: "ItemModId");

            migrationBuilder.CreateIndex(
                name: "IX_AppliedMods_ItemModSlotId",
                table: "AppliedMods",
                column: "ItemModSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_Challenges_RewardItemId",
                table: "Challenges",
                column: "RewardItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Challenges_RewardItemModId",
                table: "Challenges",
                column: "RewardItemModId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerChallenges_ChallengeId",
                table: "PlayerChallenges",
                column: "ChallengeId");

            migrationBuilder.CreateIndex(
                name: "IX_UnlockedItems_ItemId",
                table: "UnlockedItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_UnlockedMods_ItemModId",
                table: "UnlockedMods",
                column: "ItemModId");

            migrationBuilder.AddForeignKey(
                name: "FK_Players_Users_UserId",
                table: "Players",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Players_Users_UserId",
                table: "Players");

            migrationBuilder.DropTable(
                name: "AppliedMods");

            migrationBuilder.DropTable(
                name: "PlayerChallenges");

            migrationBuilder.DropTable(
                name: "PlayerStatistics");

            migrationBuilder.DropTable(
                name: "UnlockedItems");

            migrationBuilder.DropTable(
                name: "UnlockedMods");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Challenges");

            migrationBuilder.DropIndex(
                name: "IX_Players_UserId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "CurrentZoneId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "LastActivity",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Index",
                table: "ItemModSlots");

            migrationBuilder.AddColumn<string>(
                name: "PassHash",
                table: "Players",
                type: "character varying(88)",
                maxLength: 88,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "Salt",
                table: "Players",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "UserName",
                table: "Players",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "IconPath",
                table: "Items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<int>(
                name: "GuaranteedItemModId",
                table: "ItemModSlots",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Probability",
                table: "ItemModSlots",
                type: "numeric(9,8)",
                precision: 9,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "EnemyDrops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EnemyId = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    DropRate = table.Column<decimal>(type: "numeric(9,8)", precision: 9, scale: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnemyDrops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnemyDrops_Enemies_EnemyId",
                        column: x => x.EnemyId,
                        principalTable: "Enemies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EnemyDrops_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    Equipped = table.Column<bool>(type: "boolean", nullable: false),
                    InventorySlotNumber = table.Column<int>(type: "integer", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryItems_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InventoryItems_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ZoneDrops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    ZoneId = table.Column<int>(type: "integer", nullable: false),
                    DropRate = table.Column<decimal>(type: "numeric(9,8)", precision: 9, scale: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZoneDrops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZoneDrops_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ZoneDrops_Zones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryItemMods",
                columns: table => new
                {
                    InventoryItemId = table.Column<int>(type: "integer", nullable: false),
                    ItemModId = table.Column<int>(type: "integer", nullable: false),
                    ItemModSlotId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItemMods", x => new { x.InventoryItemId, x.ItemModId });
                    table.ForeignKey(
                        name: "FK_InventoryItemMods_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InventoryItemMods_ItemModSlots_ItemModSlotId",
                        column: x => x.ItemModSlotId,
                        principalTable: "ItemModSlots",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryItemMods_ItemMods_ItemModId",
                        column: x => x.ItemModId,
                        principalTable: "ItemMods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "LogSettings",
                keyColumn: "Id",
                keyValue: 3,
                column: "DefaultValue",
                value: true);

            migrationBuilder.UpdateData(
                table: "LogSettings",
                keyColumn: "Id",
                keyValue: 4,
                column: "DefaultValue",
                value: true);

            migrationBuilder.UpdateData(
                table: "LogSettings",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "DefaultValue", "Name" },
                values: new object[] { true, "Inventory" });

            migrationBuilder.UpdateData(
                table: "LogSettings",
                keyColumn: "Id",
                keyValue: 6,
                column: "DefaultValue",
                value: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemModSlots_GuaranteedItemModId",
                table: "ItemModSlots",
                column: "GuaranteedItemModId");

            migrationBuilder.CreateIndex(
                name: "IX_EnemyDrops_EnemyId",
                table: "EnemyDrops",
                column: "EnemyId");

            migrationBuilder.CreateIndex(
                name: "IX_EnemyDrops_ItemId",
                table: "EnemyDrops",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItemMods_ItemModId",
                table: "InventoryItemMods",
                column: "ItemModId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItemMods_ItemModSlotId",
                table: "InventoryItemMods",
                column: "ItemModSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_ItemId",
                table: "InventoryItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_PlayerId",
                table: "InventoryItems",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_ZoneDrops_ItemId",
                table: "ZoneDrops",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ZoneDrops_ZoneId",
                table: "ZoneDrops",
                column: "ZoneId");

            migrationBuilder.AddForeignKey(
                name: "FK_ItemModSlots_ItemMods_GuaranteedItemModId",
                table: "ItemModSlots",
                column: "GuaranteedItemModId",
                principalTable: "ItemMods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
