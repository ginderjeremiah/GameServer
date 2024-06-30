using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GameInfrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Attributes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attributes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Enemies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "0, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enemies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItemCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LogSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DefaultValue = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserName = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Salt = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PassHash = table.Column<string>(type: "nvarchar(88)", maxLength: 88, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    Exp = table.Column<int>(type: "int", nullable: false),
                    StatPointsGained = table.Column<int>(type: "int", nullable: false),
                    StatPointsUsed = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Skills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "0, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BaseDamage = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CooldownMS = table.Column<int>(type: "int", nullable: false),
                    IconPath = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SlotTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlotTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TagCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TagCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Zones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "0, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    LevelMin = table.Column<int>(type: "int", nullable: false),
                    LevelMax = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Zones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AttributeDistributions",
                columns: table => new
                {
                    EnemyId = table.Column<int>(type: "int", nullable: false),
                    AttributeId = table.Column<int>(type: "int", nullable: false),
                    BaseAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AmountPerLevel = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttributeDistributions", x => new { x.EnemyId, x.AttributeId });
                    table.ForeignKey(
                        name: "FK_AttributeDistributions_Attributes_AttributeId",
                        column: x => x.AttributeId,
                        principalTable: "Attributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttributeDistributions_Enemies_EnemyId",
                        column: x => x.EnemyId,
                        principalTable: "Enemies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ItemCategoryId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentSlots_ItemCategories_ItemCategoryId",
                        column: x => x.ItemCategoryId,
                        principalTable: "ItemCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "0, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ItemCategoryId = table.Column<int>(type: "int", nullable: false),
                    IconPath = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Items_ItemCategories_ItemCategoryId",
                        column: x => x.ItemCategoryId,
                        principalTable: "ItemCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LogPreferences",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    LogSettingId = table.Column<int>(type: "int", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogPreferences", x => new { x.PlayerId, x.LogSettingId });
                    table.ForeignKey(
                        name: "FK_LogPreferences_LogSettings_LogSettingId",
                        column: x => x.LogSettingId,
                        principalTable: "LogSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LogPreferences_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerAttributes",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    AttributeId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerAttributes", x => new { x.PlayerId, x.AttributeId });
                    table.ForeignKey(
                        name: "FK_PlayerAttributes_Attributes_AttributeId",
                        column: x => x.AttributeId,
                        principalTable: "Attributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerAttributes_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnemySkills",
                columns: table => new
                {
                    EnemyId = table.Column<int>(type: "int", nullable: false),
                    SkillId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnemySkills", x => new { x.EnemyId, x.SkillId });
                    table.ForeignKey(
                        name: "FK_EnemySkills_Enemies_EnemyId",
                        column: x => x.EnemyId,
                        principalTable: "Enemies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EnemySkills_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerSkills",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    SkillId = table.Column<int>(type: "int", nullable: false),
                    Selected = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerSkills", x => new { x.PlayerId, x.SkillId });
                    table.ForeignKey(
                        name: "FK_PlayerSkills_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerSkills_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SkillDamageMultipliers",
                columns: table => new
                {
                    SkillId = table.Column<int>(type: "int", nullable: false),
                    AttributeId = table.Column<int>(type: "int", nullable: false),
                    Multiplier = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillDamageMultipliers", x => new { x.SkillId, x.AttributeId });
                    table.ForeignKey(
                        name: "FK_SkillDamageMultipliers_Attributes_AttributeId",
                        column: x => x.AttributeId,
                        principalTable: "Attributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SkillDamageMultipliers_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemMods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "0, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Removable = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SlotTypeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemMods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemMods_SlotTypes_SlotTypeId",
                        column: x => x.SlotTypeId,
                        principalTable: "SlotTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TagCategoryId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tags_TagCategories_TagCategoryId",
                        column: x => x.TagCategoryId,
                        principalTable: "TagCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ZoneEnemies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ZoneId = table.Column<int>(type: "int", nullable: false),
                    EnemyId = table.Column<int>(type: "int", nullable: false),
                    Weight = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZoneEnemies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZoneEnemies_Enemies_EnemyId",
                        column: x => x.EnemyId,
                        principalTable: "Enemies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ZoneEnemies_Zones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnemyDrops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnemyId = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    DropRate = table.Column<decimal>(type: "decimal(9,8)", precision: 9, scale: 8, nullable: false)
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    Equipped = table.Column<bool>(type: "bit", nullable: false),
                    InventorySlotNumber = table.Column<int>(type: "int", nullable: false)
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
                name: "ItemAttributes",
                columns: table => new
                {
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    AttributeId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemAttributes", x => new { x.ItemId, x.AttributeId });
                    table.ForeignKey(
                        name: "FK_ItemAttributes_Attributes_AttributeId",
                        column: x => x.AttributeId,
                        principalTable: "Attributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemAttributes_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ZoneDrops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ZoneId = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    DropRate = table.Column<decimal>(type: "decimal(9,8)", precision: 9, scale: 8, nullable: false)
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
                name: "ItemModAttributes",
                columns: table => new
                {
                    ItemModId = table.Column<int>(type: "int", nullable: false),
                    AttributeId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemModAttributes", x => new { x.ItemModId, x.AttributeId });
                    table.ForeignKey(
                        name: "FK_ItemModAttributes_Attributes_AttributeId",
                        column: x => x.AttributeId,
                        principalTable: "Attributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemModAttributes_ItemMods_ItemModId",
                        column: x => x.ItemModId,
                        principalTable: "ItemMods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    SlotTypeId = table.Column<int>(type: "int", nullable: false),
                    GuaranteedItemModId = table.Column<int>(type: "int", nullable: true),
                    Probability = table.Column<decimal>(type: "decimal(9,8)", precision: 9, scale: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemSlots_ItemMods_GuaranteedItemModId",
                        column: x => x.GuaranteedItemModId,
                        principalTable: "ItemMods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItemSlots_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemSlots_SlotTypes_SlotTypeId",
                        column: x => x.SlotTypeId,
                        principalTable: "SlotTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemModTags",
                columns: table => new
                {
                    ItemModsId = table.Column<int>(type: "int", nullable: false),
                    TagsId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemModTags", x => new { x.ItemModsId, x.TagsId });
                    table.ForeignKey(
                        name: "FK_ItemModTags_ItemMods_ItemModsId",
                        column: x => x.ItemModsId,
                        principalTable: "ItemMods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemModTags_Tags_TagsId",
                        column: x => x.TagsId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemTags",
                columns: table => new
                {
                    ItemsId = table.Column<int>(type: "int", nullable: false),
                    TagsId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemTags", x => new { x.ItemsId, x.TagsId });
                    table.ForeignKey(
                        name: "FK_ItemTags_Items_ItemsId",
                        column: x => x.ItemsId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemTags_Tags_TagsId",
                        column: x => x.TagsId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ZoneEnemyAliases",
                columns: table => new
                {
                    ZoneEnemyId = table.Column<int>(type: "int", nullable: false),
                    AliasZoneEnemyId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZoneEnemyAliases", x => x.ZoneEnemyId);
                    table.ForeignKey(
                        name: "FK_ZoneEnemyAliases_ZoneEnemies_ZoneEnemyId",
                        column: x => x.ZoneEnemyId,
                        principalTable: "ZoneEnemies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ZoneEnemyProbabilities",
                columns: table => new
                {
                    ZoneEnemyId = table.Column<int>(type: "int", nullable: false),
                    Probability = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ZoneOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZoneEnemyProbabilities", x => x.ZoneEnemyId);
                    table.ForeignKey(
                        name: "FK_ZoneEnemyProbabilities_ZoneEnemies_ZoneEnemyId",
                        column: x => x.ZoneEnemyId,
                        principalTable: "ZoneEnemies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryItemMods",
                columns: table => new
                {
                    InventoryItemId = table.Column<int>(type: "int", nullable: false),
                    ItemModId = table.Column<int>(type: "int", nullable: false),
                    ItemSlotId = table.Column<int>(type: "int", nullable: false)
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
                        name: "FK_InventoryItemMods_ItemMods_ItemModId",
                        column: x => x.ItemModId,
                        principalTable: "ItemMods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InventoryItemMods_ItemSlots_ItemSlotId",
                        column: x => x.ItemSlotId,
                        principalTable: "ItemSlots",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "Attributes",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { 0, "A measure of one's raw physical force.", "Strength" },
                    { 1, "A measure of one's raw physical force.", "Endurance" },
                    { 2, "A measure of one's raw physical force.", "Intellect" },
                    { 3, "A measure of one's raw physical force.", "Agility" },
                    { 4, "A measure of one's raw physical force.", "Dexterity" },
                    { 5, "A measure of one's raw physical force.", "Luck" },
                    { 6, "A measure of one's raw physical force.", "Max Health" },
                    { 7, "A measure of one's raw physical force.", "Defense" },
                    { 8, "A measure of one's raw physical force.", "Cooldown Recovery" },
                    { 9, "A measure of one's raw physical force.", "Drop Bonus" },
                    { 10, "A measure of one's raw physical force.", "Critical Chance" },
                    { 11, "A measure of one's raw physical force.", "Critical Damage" },
                    { 12, "A measure of one's raw physical force.", "Dodge Chance" },
                    { 13, "A measure of one's raw physical force.", "Block Chance" },
                    { 14, "A measure of one's raw physical force.", "Block Reduction" }
                });

            migrationBuilder.InsertData(
                table: "ItemCategories",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Helm" },
                    { 2, "Chest" },
                    { 3, "Leg" },
                    { 4, "Boot" },
                    { 5, "Weapon" },
                    { 6, "Accessory" }
                });

            migrationBuilder.InsertData(
                table: "LogSettings",
                columns: new[] { "Id", "DefaultValue", "Name" },
                values: new object[,]
                {
                    { 1, false, "Damage" },
                    { 2, false, "Debug" },
                    { 3, true, "Exp" },
                    { 4, true, "Level Up" },
                    { 5, true, "Inventory" },
                    { 6, true, "Enemy Defeated" }
                });

            migrationBuilder.InsertData(
                table: "SlotTypes",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Component" },
                    { 2, "Prefix" },
                    { 3, "Suffix" }
                });

            migrationBuilder.InsertData(
                table: "TagCategories",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Acessory" },
                    { 2, "Armor" },
                    { 3, "Magical" },
                    { 4, "Material" },
                    { 5, "Modification" },
                    { 6, "Usage" },
                    { 7, "Weapon" }
                });

            migrationBuilder.InsertData(
                table: "EquipmentSlots",
                columns: new[] { "Id", "ItemCategoryId", "Name" },
                values: new object[,]
                {
                    { 0, 1, "Helm Slot" },
                    { 1, 2, "Chest Slot" },
                    { 2, 3, "Leg Slot" },
                    { 3, 4, "Boot Slot" },
                    { 4, 5, "Weapon Slot" },
                    { 5, 6, "Accessory Slot" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttributeDistributions_AttributeId",
                table: "AttributeDistributions",
                column: "AttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_EnemyDrops_EnemyId",
                table: "EnemyDrops",
                column: "EnemyId");

            migrationBuilder.CreateIndex(
                name: "IX_EnemyDrops_ItemId",
                table: "EnemyDrops",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_EnemySkills_SkillId",
                table: "EnemySkills",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentSlots_ItemCategoryId",
                table: "EquipmentSlots",
                column: "ItemCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItemMods_ItemModId",
                table: "InventoryItemMods",
                column: "ItemModId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItemMods_ItemSlotId",
                table: "InventoryItemMods",
                column: "ItemSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_ItemId",
                table: "InventoryItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_PlayerId",
                table: "InventoryItems",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemAttributes_AttributeId",
                table: "ItemAttributes",
                column: "AttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemModAttributes_AttributeId",
                table: "ItemModAttributes",
                column: "AttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemMods_SlotTypeId",
                table: "ItemMods",
                column: "SlotTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemModTags_TagsId",
                table: "ItemModTags",
                column: "TagsId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_ItemCategoryId",
                table: "Items",
                column: "ItemCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSlots_GuaranteedItemModId",
                table: "ItemSlots",
                column: "GuaranteedItemModId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSlots_ItemId",
                table: "ItemSlots",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSlots_SlotTypeId",
                table: "ItemSlots",
                column: "SlotTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemTags_TagsId",
                table: "ItemTags",
                column: "TagsId");

            migrationBuilder.CreateIndex(
                name: "IX_LogPreferences_LogSettingId",
                table: "LogPreferences",
                column: "LogSettingId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerAttributes_AttributeId",
                table: "PlayerAttributes",
                column: "AttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSkills_SkillId",
                table: "PlayerSkills",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillDamageMultipliers_AttributeId",
                table: "SkillDamageMultipliers",
                column: "AttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_TagCategoryId",
                table: "Tags",
                column: "TagCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ZoneDrops_ItemId",
                table: "ZoneDrops",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ZoneDrops_ZoneId",
                table: "ZoneDrops",
                column: "ZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_ZoneEnemies_EnemyId",
                table: "ZoneEnemies",
                column: "EnemyId");

            migrationBuilder.CreateIndex(
                name: "IX_ZoneEnemies_ZoneId",
                table: "ZoneEnemies",
                column: "ZoneId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttributeDistributions");

            migrationBuilder.DropTable(
                name: "EnemyDrops");

            migrationBuilder.DropTable(
                name: "EnemySkills");

            migrationBuilder.DropTable(
                name: "EquipmentSlots");

            migrationBuilder.DropTable(
                name: "InventoryItemMods");

            migrationBuilder.DropTable(
                name: "ItemAttributes");

            migrationBuilder.DropTable(
                name: "ItemModAttributes");

            migrationBuilder.DropTable(
                name: "ItemModTags");

            migrationBuilder.DropTable(
                name: "ItemTags");

            migrationBuilder.DropTable(
                name: "LogPreferences");

            migrationBuilder.DropTable(
                name: "PlayerAttributes");

            migrationBuilder.DropTable(
                name: "PlayerSkills");

            migrationBuilder.DropTable(
                name: "SkillDamageMultipliers");

            migrationBuilder.DropTable(
                name: "ZoneDrops");

            migrationBuilder.DropTable(
                name: "ZoneEnemyAliases");

            migrationBuilder.DropTable(
                name: "ZoneEnemyProbabilities");

            migrationBuilder.DropTable(
                name: "InventoryItems");

            migrationBuilder.DropTable(
                name: "ItemSlots");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "LogSettings");

            migrationBuilder.DropTable(
                name: "Attributes");

            migrationBuilder.DropTable(
                name: "Skills");

            migrationBuilder.DropTable(
                name: "ZoneEnemies");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "ItemMods");

            migrationBuilder.DropTable(
                name: "Items");

            migrationBuilder.DropTable(
                name: "TagCategories");

            migrationBuilder.DropTable(
                name: "Enemies");

            migrationBuilder.DropTable(
                name: "Zones");

            migrationBuilder.DropTable(
                name: "SlotTypes");

            migrationBuilder.DropTable(
                name: "ItemCategories");
        }
    }
}
