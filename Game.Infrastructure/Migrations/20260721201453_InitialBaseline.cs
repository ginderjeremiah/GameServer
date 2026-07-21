using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Attributes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attributes", x => x.Id);
                });

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
                name: "Classes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'0', '1', '0', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Word = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PassiveAttributeId = table.Column<int>(type: "integer", nullable: false),
                    PassiveAmount = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    PassiveScalingAttributeId = table.Column<int>(type: "integer", nullable: true),
                    PassiveScalingAmount = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    PassiveModifierType = table.Column<int>(type: "integer", nullable: false),
                    DesignerNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RetiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Classes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Enemies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'0', '1', '0', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsBoss = table.Column<bool>(type: "boolean", nullable: false),
                    DesignerNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RetiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enemies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItemCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItemModTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemModTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Lessons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'0', '1', '0', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TriggerType = table.Column<int>(type: "integer", nullable: false),
                    ScreenKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TriggerMechanicEvent = table.Column<int>(type: "integer", nullable: true),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    DesignerNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RetiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lessons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LogTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DefaultValue = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Paths",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'0', '1', '0', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ActivityKey = table.Column<int>(type: "integer", nullable: false),
                    DesignerNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RetiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Paths", x => x.Id);
                });

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

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StatisticTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    EntityType = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatisticTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TagCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TagCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PassHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LastLogin = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BannedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
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
                name: "ClassAttributeDistributions",
                columns: table => new
                {
                    ClassId = table.Column<int>(type: "integer", nullable: false),
                    AttributeId = table.Column<int>(type: "integer", nullable: false),
                    BaseAmount = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    AmountPerLevel = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassAttributeDistributions", x => new { x.ClassId, x.AttributeId });
                    table.ForeignKey(
                        name: "FK_ClassAttributeDistributions_Attributes_AttributeId",
                        column: x => x.AttributeId,
                        principalTable: "Attributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassAttributeDistributions_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AttributeDistributions",
                columns: table => new
                {
                    EnemyId = table.Column<int>(type: "integer", nullable: false),
                    AttributeId = table.Column<int>(type: "integer", nullable: false),
                    BaseAmount = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    AmountPerLevel = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ItemCategoryId = table.Column<int>(type: "integer", nullable: false)
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
                name: "LessonSteps",
                columns: table => new
                {
                    LessonId = table.Column<int>(type: "integer", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    AnchorKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonSteps", x => new { x.LessonId, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_LessonSteps_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Proficiencies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'0', '1', '0', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IconPath = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Word = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Pronunciation = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Translation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PathId = table.Column<int>(type: "integer", nullable: false),
                    PathOrdinal = table.Column<int>(type: "integer", nullable: false),
                    MaxLevel = table.Column<int>(type: "integer", nullable: false),
                    BaseXp = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    XpGrowth = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    DesignerNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RetiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proficiencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Proficiencies_Paths_PathId",
                        column: x => x.PathId,
                        principalTable: "Paths",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemMods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'0', '1', '0', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ItemModTypeId = table.Column<int>(type: "integer", nullable: false),
                    RarityId = table.Column<int>(type: "integer", nullable: false),
                    DesignerNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RetiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemMods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemMods_ItemModTypes_ItemModTypeId",
                        column: x => x.ItemModTypeId,
                        principalTable: "ItemModTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemMods_Rarities_RarityId",
                        column: x => x.RarityId,
                        principalTable: "Rarities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Skills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'0', '1', '0', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BaseDamage = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CooldownMs = table.Column<int>(type: "integer", nullable: false),
                    CriticalChance = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    IconPath = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RarityId = table.Column<int>(type: "integer", nullable: false),
                    Word = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Pronunciation = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Translation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Acquisition = table.Column<int>(type: "integer", nullable: false),
                    DesignerNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RetiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Skills_Rarities_RarityId",
                        column: x => x.RarityId,
                        principalTable: "Rarities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChallengeTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StatisticTypeId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChallengeTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChallengeTypes_StatisticTypes_StatisticTypeId",
                        column: x => x.StatisticTypeId,
                        principalTable: "StatisticTypes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TagCategoryId = table.Column<int>(type: "integer", nullable: false)
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
                name: "UserRoles",
                columns: table => new
                {
                    RolesId = table.Column<int>(type: "integer", nullable: false),
                    UsersId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.RolesId, x.UsersId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RolesId",
                        column: x => x.RolesId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UsersId",
                        column: x => x.UsersId,
                        principalTable: "Users",
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

            migrationBuilder.CreateTable(
                name: "ProficiencyLevelModifiers",
                columns: table => new
                {
                    ProficiencyId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    AttributeId = table.Column<int>(type: "integer", nullable: false),
                    ModifierType = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProficiencyLevelModifiers", x => new { x.ProficiencyId, x.Level, x.AttributeId });
                    table.ForeignKey(
                        name: "FK_ProficiencyLevelModifiers_Attributes_AttributeId",
                        column: x => x.AttributeId,
                        principalTable: "Attributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProficiencyLevelModifiers_Proficiencies_ProficiencyId",
                        column: x => x.ProficiencyId,
                        principalTable: "Proficiencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProficiencyPrerequisites",
                columns: table => new
                {
                    ProficiencyId = table.Column<int>(type: "integer", nullable: false),
                    PrerequisiteProficiencyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProficiencyPrerequisites", x => new { x.ProficiencyId, x.PrerequisiteProficiencyId });
                    table.ForeignKey(
                        name: "FK_ProficiencyPrerequisites_Proficiencies_PrerequisiteProficie~",
                        column: x => x.PrerequisiteProficiencyId,
                        principalTable: "Proficiencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProficiencyPrerequisites_Proficiencies_ProficiencyId",
                        column: x => x.ProficiencyId,
                        principalTable: "Proficiencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemModAttributes",
                columns: table => new
                {
                    ItemModId = table.Column<int>(type: "integer", nullable: false),
                    AttributeId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
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
                name: "ClassStarterSkills",
                columns: table => new
                {
                    ClassId = table.Column<int>(type: "integer", nullable: false),
                    SkillId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassStarterSkills", x => new { x.ClassId, x.SkillId });
                    table.ForeignKey(
                        name: "FK_ClassStarterSkills_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassStarterSkills_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnemySkills",
                columns: table => new
                {
                    EnemyId = table.Column<int>(type: "integer", nullable: false),
                    SkillId = table.Column<int>(type: "integer", nullable: false)
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
                name: "Items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'0', '1', '0', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ItemCategoryId = table.Column<int>(type: "integer", nullable: false),
                    IconPath = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RarityId = table.Column<int>(type: "integer", nullable: false),
                    GrantedSkillId = table.Column<int>(type: "integer", nullable: true),
                    WeaponType = table.Column<int>(type: "integer", nullable: true),
                    RequiredProficiencyId = table.Column<int>(type: "integer", nullable: true),
                    RequiredProficiencyLevel = table.Column<int>(type: "integer", nullable: false),
                    DesignerNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RetiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                    table.ForeignKey(
                        name: "FK_Items_Proficiencies_RequiredProficiencyId",
                        column: x => x.RequiredProficiencyId,
                        principalTable: "Proficiencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Items_Rarities_RarityId",
                        column: x => x.RarityId,
                        principalTable: "Rarities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Items_Skills_GrantedSkillId",
                        column: x => x.GrantedSkillId,
                        principalTable: "Skills",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProficiencyLevelRewards",
                columns: table => new
                {
                    ProficiencyId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    RewardSkillId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProficiencyLevelRewards", x => new { x.ProficiencyId, x.Level });
                    table.ForeignKey(
                        name: "FK_ProficiencyLevelRewards_Proficiencies_ProficiencyId",
                        column: x => x.ProficiencyId,
                        principalTable: "Proficiencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProficiencyLevelRewards_Skills_RewardSkillId",
                        column: x => x.RewardSkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SkillDamageMultipliers",
                columns: table => new
                {
                    SkillId = table.Column<int>(type: "integer", nullable: false),
                    AttributeId = table.Column<int>(type: "integer", nullable: false),
                    Multiplier = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
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
                name: "SkillDamagePortions",
                columns: table => new
                {
                    SkillId = table.Column<int>(type: "integer", nullable: false),
                    DamageType = table.Column<int>(type: "integer", nullable: false),
                    Weight = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillDamagePortions", x => new { x.SkillId, x.DamageType });
                    table.ForeignKey(
                        name: "FK_SkillDamagePortions_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SkillEffects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SkillId = table.Column<int>(type: "integer", nullable: false),
                    Target = table.Column<int>(type: "integer", nullable: false),
                    AttributeId = table.Column<int>(type: "integer", nullable: false),
                    ModifierType = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    ScalingAttributeId = table.Column<int>(type: "integer", nullable: false),
                    ScalingAmount = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillEffects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SkillEffects_Attributes_AttributeId",
                        column: x => x.AttributeId,
                        principalTable: "Attributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SkillEffects_Attributes_ScalingAttributeId",
                        column: x => x.ScalingAttributeId,
                        principalTable: "Attributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SkillEffects_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SkillRecipes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'0', '1', '0', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ResultSkillId = table.Column<int>(type: "integer", nullable: false),
                    DesignerNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RetiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillRecipes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SkillRecipes_Skills_ResultSkillId",
                        column: x => x.ResultSkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ItemModTags",
                columns: table => new
                {
                    ItemModId = table.Column<int>(type: "integer", nullable: false),
                    TagId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemModTags", x => new { x.ItemModId, x.TagId });
                    table.ForeignKey(
                        name: "FK_ItemModTags_ItemMods_ItemModId",
                        column: x => x.ItemModId,
                        principalTable: "ItemMods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemModTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
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
                    ProgressGoal = table.Column<decimal>(type: "numeric(36,3)", precision: 36, scale: 3, nullable: false),
                    RewardItemId = table.Column<int>(type: "integer", nullable: true),
                    RewardItemModId = table.Column<int>(type: "integer", nullable: true),
                    DesignerNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RetiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Challenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Challenges_ChallengeTypes_ChallengeTypeId",
                        column: x => x.ChallengeTypeId,
                        principalTable: "ChallengeTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "ClassStarterEquipment",
                columns: table => new
                {
                    ClassId = table.Column<int>(type: "integer", nullable: false),
                    EquipmentSlotId = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassStarterEquipment", x => new { x.ClassId, x.EquipmentSlotId });
                    table.ForeignKey(
                        name: "FK_ClassStarterEquipment_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassStarterEquipment_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemAttributes",
                columns: table => new
                {
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    AttributeId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
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
                name: "ItemModSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    ItemModSlotTypeId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemModSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemModSlots_ItemModTypes_ItemModSlotTypeId",
                        column: x => x.ItemModSlotTypeId,
                        principalTable: "ItemModTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemModSlots_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemTags",
                columns: table => new
                {
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    TagId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemTags", x => new { x.ItemId, x.TagId });
                    table.ForeignKey(
                        name: "FK_ItemTags_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SkillRecipeConditions",
                columns: table => new
                {
                    RecipeId = table.Column<int>(type: "integer", nullable: false),
                    ProficiencyId = table.Column<int>(type: "integer", nullable: false),
                    MinLevel = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillRecipeConditions", x => new { x.RecipeId, x.ProficiencyId });
                    table.ForeignKey(
                        name: "FK_SkillRecipeConditions_Proficiencies_ProficiencyId",
                        column: x => x.ProficiencyId,
                        principalTable: "Proficiencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SkillRecipeConditions_SkillRecipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "SkillRecipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SkillRecipeInputs",
                columns: table => new
                {
                    RecipeId = table.Column<int>(type: "integer", nullable: false),
                    SkillId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillRecipeInputs", x => new { x.RecipeId, x.SkillId });
                    table.ForeignKey(
                        name: "FK_SkillRecipeInputs_SkillRecipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "SkillRecipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SkillRecipeInputs_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Zones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'0', '1', '0', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    LevelMin = table.Column<int>(type: "integer", nullable: false),
                    LevelMax = table.Column<int>(type: "integer", nullable: false),
                    BossEnemyId = table.Column<int>(type: "integer", nullable: true),
                    BossLevel = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    UnlockChallengeId = table.Column<int>(type: "integer", nullable: true),
                    IsHome = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DesignerNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RetiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Zones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Zones_Challenges_UnlockChallengeId",
                        column: x => x.UnlockChallengeId,
                        principalTable: "Challenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Zones_Enemies_BossEnemyId",
                        column: x => x.BossEnemyId,
                        principalTable: "Enemies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CurrentZoneId = table.Column<int>(type: "integer", nullable: false),
                    ClassId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Exp = table.Column<int>(type: "integer", nullable: false),
                    StatPointsGained = table.Column<int>(type: "integer", nullable: false),
                    StatPointsUsed = table.Column<int>(type: "integer", nullable: false),
                    LastActivity = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AutoChallengeBoss = table.Column<bool>(type: "boolean", nullable: false),
                    LastCreditedBattleSeed = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Players_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Players_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Players_Zones_CurrentZoneId",
                        column: x => x.CurrentZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ZoneEnemies",
                columns: table => new
                {
                    ZoneId = table.Column<int>(type: "integer", nullable: false),
                    EnemyId = table.Column<int>(type: "integer", nullable: false),
                    Weight = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZoneEnemies", x => new { x.ZoneId, x.EnemyId });
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
                        onDelete: ReferentialAction.Restrict);
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
                name: "LogPreferences",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    LogTypeId = table.Column<int>(type: "integer", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogPreferences", x => new { x.PlayerId, x.LogTypeId });
                    table.ForeignKey(
                        name: "FK_LogPreferences_LogTypes_LogTypeId",
                        column: x => x.LogTypeId,
                        principalTable: "LogTypes",
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
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    AttributeId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
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
                name: "PlayerChallenges",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    ChallengeId = table.Column<int>(type: "integer", nullable: false),
                    Progress = table.Column<decimal>(type: "numeric(36,3)", precision: 36, scale: 3, nullable: false),
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

            migrationBuilder.CreateTable(
                name: "PlayerLessons",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    LessonId = table.Column<int>(type: "integer", nullable: false),
                    UnlockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerLessons", x => new { x.PlayerId, x.LessonId });
                    table.ForeignKey(
                        name: "FK_PlayerLessons_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerLessons_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerProficiencies",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    ProficiencyId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Xp = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerProficiencies", x => new { x.PlayerId, x.ProficiencyId });
                    table.ForeignKey(
                        name: "FK_PlayerProficiencies_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerProficiencies_Proficiencies_ProficiencyId",
                        column: x => x.ProficiencyId,
                        principalTable: "Proficiencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerSkills",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    SkillId = table.Column<int>(type: "integer", nullable: false),
                    Selected = table.Column<bool>(type: "boolean", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
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
                name: "PlayerStatistics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    StatisticTypeId = table.Column<int>(type: "integer", nullable: false),
                    EntityId = table.Column<int>(type: "integer", nullable: true),
                    Value = table.Column<decimal>(type: "numeric(36,3)", precision: 36, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerStatistics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerStatistics_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerStatistics_StatisticTypes_StatisticTypeId",
                        column: x => x.StatisticTypeId,
                        principalTable: "StatisticTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UnlockedItems",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    EquipmentSlotId = table.Column<int>(type: "integer", nullable: true),
                    Favorite = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnlockedItems", x => new { x.PlayerId, x.ItemId });
                    table.ForeignKey(
                        name: "FK_UnlockedItems_EquipmentSlots_EquipmentSlotId",
                        column: x => x.EquipmentSlotId,
                        principalTable: "EquipmentSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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

            migrationBuilder.InsertData(
                table: "Attributes",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { 0, "A measure of one's raw physical force. Increases the damage of some physical skills and contributes to maximum health.", "Strength" },
                    { 1, "A measure of one's resilience and physical fortitude. Contributes to maximum health and toughness.", "Endurance" },
                    { 2, "A measure of one's mental acuity and command of the arcane. Increases the damage of magical skills.", "Intellect" },
                    { 3, "A measure of one's speed and reflexes. Amplifies your cooldown bonus and dodge chance.", "Agility" },
                    { 4, "A measure of one's precision and finesse. Increases the damage of some physical skills.", "Dexterity" },
                    { 5, "A measure of one's fortune, influencing various chance-based outcomes.", "Luck" },
                    { 6, "The amount of health a character has at the start of a battle.", "Max Health" },
                    { 7, "Reduces all incoming direct damage by a percentage that grows with diminishing returns, never reaching full immunity.", "Toughness" },
                    { 8, "A percentage multiplier to the rate at which skills become available again after being used.", "Cooldown Recovery" },
                    { 9, "Obsolete. Previously increased the rate at which items were dropped by enemies.", "Drop Bonus" },
                    { 10, "A multiplier applied to a skill's own base critical-hit chance.", "Critical Chance Multiplier" },
                    { 11, "The additional percentage of damage dealt when a critical hit occurs.", "Critical Damage" },
                    { 12, "The percentage chance to completely avoid the damage from an incoming attack.", "Dodge Chance" },
                    { 15, "The amount of bleed damage taken each second from damage-over-time effects.", "Bleed Damage Per Second" },
                    { 16, "The amount of health restored each second from heal-over-time effects.", "Health Regen Per Second" },
                    { 17, "Increases the damage your physical attacks deal.", "Physical Amplification" },
                    { 18, "Reduces the damage you take from physical attacks.", "Physical Resistance" },
                    { 19, "Increases the damage your fire attacks deal.", "Fire Amplification" },
                    { 20, "Reduces the damage you take from fire attacks.", "Fire Resistance" },
                    { 21, "Increases the damage your water attacks deal.", "Water Amplification" },
                    { 22, "Reduces the damage you take from water attacks.", "Water Resistance" },
                    { 23, "Increases the damage your earth attacks deal.", "Earth Amplification" },
                    { 24, "Reduces the damage you take from earth attacks.", "Earth Resistance" },
                    { 25, "Increases the damage your wind attacks deal.", "Wind Amplification" },
                    { 26, "Reduces the damage you take from wind attacks.", "Wind Resistance" },
                    { 27, "Increases the damage your bleed attacks deal.", "Bleed Amplification" },
                    { 28, "Reduces the damage you take from bleed attacks.", "Bleed Resistance" },
                    { 29, "Increases the damage your poison attacks deal.", "Poison Amplification" },
                    { 30, "Reduces the damage you take from poison attacks.", "Poison Resistance" },
                    { 31, "Increases the damage your burn attacks deal.", "Burn Amplification" },
                    { 32, "Reduces the damage you take from burn attacks.", "Burn Resistance" },
                    { 33, "Increases the damage your elemental attacks deal.", "Elemental Amplification" },
                    { 34, "Reduces the damage you take from elemental attacks.", "Elemental Resistance" },
                    { 35, "Increases the damage your damage-over-time attacks deal.", "Dot Amplification" },
                    { 36, "Reduces the damage you take from damage-over-time attacks.", "Dot Resistance" },
                    { 37, "The amount of poison damage taken each second from damage-over-time effects.", "Poison Damage Per Second" },
                    { 38, "The amount of burn damage taken each second from damage-over-time effects.", "Burn Damage Per Second" },
                    { 39, "Increases the damage your sword attacks deal.", "Sword Amplification" },
                    { 40, "Increases the damage your axe attacks deal.", "Axe Amplification" },
                    { 41, "Increases the damage your bow attacks deal.", "Bow Amplification" },
                    { 42, "Increases the damage your club attacks deal.", "Club Amplification" },
                    { 43, "Increases the damage your dagger attacks deal.", "Dagger Amplification" },
                    { 44, "Increases the damage your unarmed attacks deal.", "Unarmed Amplification" },
                    { 45, "The percentage of a direct hit's damage returned to the attacker, ignoring their defenses.", "Damage Reflection" },
                    { 46, "The maximum percentage of bonus damage dealt against a target, scaled by how much health it is missing.", "Execute Bonus" },
                    { 47, "The percentage chance to parry an incoming attack, negating it and striking back with the equipped weapon's signature skill.", "Parry Chance" },
                    { 48, "A multiplier applied to your parry chance.", "Parry Chance Multiplier" },
                    { 49, "A multiplier applied to your dodge chance.", "Dodge Chance Multiplier" },
                    { 50, "Additional cooldown recovery granted by items and skill effects, amplified by your cooldown bonus multiplier.", "Cooldown Bonus" },
                    { 51, "A multiplier applied to your cooldown bonus.", "Cooldown Bonus Multiplier" }
                });

            migrationBuilder.InsertData(
                table: "ChallengeTypes",
                columns: new[] { "Id", "Name", "StatisticTypeId" },
                values: new object[] { 5, "Level Reached", null });

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
                table: "ItemModTypes",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Component" },
                    { 2, "Prefix" },
                    { 3, "Suffix" }
                });

            migrationBuilder.InsertData(
                table: "LogTypes",
                columns: new[] { "Id", "DefaultValue", "Name" },
                values: new object[,]
                {
                    { 1, false, "Damage" },
                    { 2, false, "Debug" },
                    { 3, false, "Exp" },
                    { 4, false, "Level Up" },
                    { 5, false, "Item Found" },
                    { 6, false, "Enemy Defeated" },
                    { 7, false, "Skill Effect" },
                    { 8, false, "Proficiency" }
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

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Name" },
                values: new object[] { 1, "Admin" });

            migrationBuilder.InsertData(
                table: "StatisticTypes",
                columns: new[] { "Id", "EntityType", "Name" },
                values: new object[,]
                {
                    { 1, 1, "Enemies Killed" },
                    { 2, 1, "Bosses Defeated" },
                    { 3, 2, "Zones Cleared" },
                    { 4, 3, "Damage Dealt" },
                    { 5, 3, "Highest Single Attack Damage" },
                    { 6, 0, "Damage Taken" },
                    { 7, 0, "Damage Healed" },
                    { 8, 1, "Enemies Encountered" },
                    { 9, 1, "Battles Won" },
                    { 10, 1, "Battles Lost" },
                    { 11, 0, "Player Deaths" },
                    { 12, 0, "Total Battle Time" },
                    { 13, 1, "Fastest Victory" },
                    { 14, 3, "Skills Used" },
                    { 15, 1, "Battles Abandoned" },
                    { 16, 0, "Critical Hits" },
                    { 17, 0, "Critical Damage Dealt" },
                    { 18, 0, "Attacks Dodged" },
                    { 19, 0, "Damage Dodged" },
                    { 22, 4, "Kills By Damage Type" },
                    { 23, 0, "Attacks Parried" },
                    { 24, 0, "Damage Parried" },
                    { 25, 0, "Counter Damage Dealt" }
                });

            migrationBuilder.InsertData(
                table: "TagCategories",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Accessory" },
                    { 2, "Armor" },
                    { 3, "Magical" },
                    { 4, "Material" },
                    { 5, "Modification" },
                    { 6, "Usage" },
                    { 7, "Weapon" }
                });

            migrationBuilder.InsertData(
                table: "ChallengeTypes",
                columns: new[] { "Id", "Name", "StatisticTypeId" },
                values: new object[,]
                {
                    { 1, "Enemies Killed", 1 },
                    { 2, "Bosses Defeated", 2 },
                    { 3, "Zones Cleared", 3 },
                    { 4, "Time Trial", 13 },
                    { 6, "Damage Dealt", 4 },
                    { 7, "Battles Won", 9 },
                    { 8, "Skills Used", 14 },
                    { 9, "Kills By Damage Type", 22 }
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
                name: "IX_AttributeDistributions_AttributeId",
                table: "AttributeDistributions",
                column: "AttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_BrowserInfos_UserAgent",
                table: "BrowserInfos",
                column: "UserAgent",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Challenges_ChallengeTypeId",
                table: "Challenges",
                column: "ChallengeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Challenges_RewardItemId",
                table: "Challenges",
                column: "RewardItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Challenges_RewardItemModId",
                table: "Challenges",
                column: "RewardItemModId");

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeTypes_StatisticTypeId",
                table: "ChallengeTypes",
                column: "StatisticTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassAttributeDistributions_AttributeId",
                table: "ClassAttributeDistributions",
                column: "AttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassStarterEquipment_ItemId",
                table: "ClassStarterEquipment",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassStarterSkills_SkillId",
                table: "ClassStarterSkills",
                column: "SkillId");

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
                name: "IX_EnemySkills_SkillId",
                table: "EnemySkills",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentSlots_ItemCategoryId",
                table: "EquipmentSlots",
                column: "ItemCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemAttributes_AttributeId",
                table: "ItemAttributes",
                column: "AttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemModAttributes_AttributeId",
                table: "ItemModAttributes",
                column: "AttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemMods_ItemModTypeId",
                table: "ItemMods",
                column: "ItemModTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemMods_RarityId",
                table: "ItemMods",
                column: "RarityId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemModSlots_ItemId",
                table: "ItemModSlots",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemModSlots_ItemModSlotTypeId",
                table: "ItemModSlots",
                column: "ItemModSlotTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemModTags_TagId",
                table: "ItemModTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_GrantedSkillId",
                table: "Items",
                column: "GrantedSkillId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_ItemCategoryId",
                table: "Items",
                column: "ItemCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_RarityId",
                table: "Items",
                column: "RarityId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_RequiredProficiencyId",
                table: "Items",
                column: "RequiredProficiencyId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemTags_TagId",
                table: "ItemTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_Lessons_Key",
                table: "Lessons",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LogPreferences_LogTypeId",
                table: "LogPreferences",
                column: "LogTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerAttributes_AttributeId",
                table: "PlayerAttributes",
                column: "AttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerChallenges_ChallengeId",
                table: "PlayerChallenges",
                column: "ChallengeId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerLessons_LessonId",
                table: "PlayerLessons",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerProficiencies_ProficiencyId",
                table: "PlayerProficiencies",
                column: "ProficiencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_ClassId",
                table: "Players",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_CurrentZoneId",
                table: "Players",
                column: "CurrentZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_UserId",
                table: "Players",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSkills_SkillId",
                table: "PlayerSkills",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerStatistics_PlayerId_StatisticTypeId_EntityId",
                table: "PlayerStatistics",
                columns: new[] { "PlayerId", "StatisticTypeId", "EntityId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerStatistics_StatisticTypeId",
                table: "PlayerStatistics",
                column: "StatisticTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Proficiencies_PathId_PathOrdinal",
                table: "Proficiencies",
                columns: new[] { "PathId", "PathOrdinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProficiencyLevelModifiers_AttributeId",
                table: "ProficiencyLevelModifiers",
                column: "AttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProficiencyLevelRewards_RewardSkillId",
                table: "ProficiencyLevelRewards",
                column: "RewardSkillId");

            migrationBuilder.CreateIndex(
                name: "IX_ProficiencyPrerequisites_PrerequisiteProficiencyId",
                table: "ProficiencyPrerequisites",
                column: "PrerequisiteProficiencyId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillDamageMultipliers_AttributeId",
                table: "SkillDamageMultipliers",
                column: "AttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillEffects_AttributeId",
                table: "SkillEffects",
                column: "AttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillEffects_ScalingAttributeId",
                table: "SkillEffects",
                column: "ScalingAttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillEffects_SkillId",
                table: "SkillEffects",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillRecipeConditions_ProficiencyId",
                table: "SkillRecipeConditions",
                column: "ProficiencyId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillRecipeInputs_SkillId",
                table: "SkillRecipeInputs",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillRecipes_ResultSkillId",
                table: "SkillRecipes",
                column: "ResultSkillId");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_RarityId",
                table: "Skills",
                column: "RarityId");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_TagCategoryId",
                table: "Tags",
                column: "TagCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_UnlockedItems_EquipmentSlotId",
                table: "UnlockedItems",
                column: "EquipmentSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_UnlockedItems_ItemId",
                table: "UnlockedItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_UnlockedItems_PlayerId_EquipmentSlotId",
                table: "UnlockedItems",
                columns: new[] { "PlayerId", "EquipmentSlotId" },
                unique: true,
                filter: "\"EquipmentSlotId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UnlockedMods_ItemModId",
                table: "UnlockedMods",
                column: "ItemModId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLogins_DeviceId",
                table: "UserLogins",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLogins_UserId_IpAddress_DeviceId",
                table: "UserLogins",
                columns: new[] { "UserId", "IpAddress", "DeviceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UsersId",
                table: "UserRoles",
                column: "UsersId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true,
                filter: "\"ArchivedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ZoneEnemies_EnemyId",
                table: "ZoneEnemies",
                column: "EnemyId");

            migrationBuilder.CreateIndex(
                name: "IX_Zones_BossEnemyId",
                table: "Zones",
                column: "BossEnemyId");

            migrationBuilder.CreateIndex(
                name: "IX_Zones_UnlockChallengeId",
                table: "Zones",
                column: "UnlockChallengeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppliedMods");

            migrationBuilder.DropTable(
                name: "AttributeDistributions");

            migrationBuilder.DropTable(
                name: "ClassAttributeDistributions");

            migrationBuilder.DropTable(
                name: "ClassStarterEquipment");

            migrationBuilder.DropTable(
                name: "ClassStarterSkills");

            migrationBuilder.DropTable(
                name: "EnemySkills");

            migrationBuilder.DropTable(
                name: "ItemAttributes");

            migrationBuilder.DropTable(
                name: "ItemModAttributes");

            migrationBuilder.DropTable(
                name: "ItemModTags");

            migrationBuilder.DropTable(
                name: "ItemTags");

            migrationBuilder.DropTable(
                name: "LessonSteps");

            migrationBuilder.DropTable(
                name: "LogPreferences");

            migrationBuilder.DropTable(
                name: "PlayerAttributes");

            migrationBuilder.DropTable(
                name: "PlayerChallenges");

            migrationBuilder.DropTable(
                name: "PlayerLessons");

            migrationBuilder.DropTable(
                name: "PlayerProficiencies");

            migrationBuilder.DropTable(
                name: "PlayerSkills");

            migrationBuilder.DropTable(
                name: "PlayerStatistics");

            migrationBuilder.DropTable(
                name: "ProficiencyLevelModifiers");

            migrationBuilder.DropTable(
                name: "ProficiencyLevelRewards");

            migrationBuilder.DropTable(
                name: "ProficiencyPrerequisites");

            migrationBuilder.DropTable(
                name: "SkillDamageMultipliers");

            migrationBuilder.DropTable(
                name: "SkillDamagePortions");

            migrationBuilder.DropTable(
                name: "SkillEffects");

            migrationBuilder.DropTable(
                name: "SkillRecipeConditions");

            migrationBuilder.DropTable(
                name: "SkillRecipeInputs");

            migrationBuilder.DropTable(
                name: "UnlockedItems");

            migrationBuilder.DropTable(
                name: "UnlockedMods");

            migrationBuilder.DropTable(
                name: "UserLogins");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "ZoneEnemies");

            migrationBuilder.DropTable(
                name: "ItemModSlots");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "LogTypes");

            migrationBuilder.DropTable(
                name: "Lessons");

            migrationBuilder.DropTable(
                name: "Attributes");

            migrationBuilder.DropTable(
                name: "SkillRecipes");

            migrationBuilder.DropTable(
                name: "EquipmentSlots");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "TagCategories");

            migrationBuilder.DropTable(
                name: "Classes");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Zones");

            migrationBuilder.DropTable(
                name: "BrowserInfos");

            migrationBuilder.DropTable(
                name: "Challenges");

            migrationBuilder.DropTable(
                name: "Enemies");

            migrationBuilder.DropTable(
                name: "ChallengeTypes");

            migrationBuilder.DropTable(
                name: "ItemMods");

            migrationBuilder.DropTable(
                name: "Items");

            migrationBuilder.DropTable(
                name: "StatisticTypes");

            migrationBuilder.DropTable(
                name: "ItemModTypes");

            migrationBuilder.DropTable(
                name: "ItemCategories");

            migrationBuilder.DropTable(
                name: "Proficiencies");

            migrationBuilder.DropTable(
                name: "Skills");

            migrationBuilder.DropTable(
                name: "Paths");

            migrationBuilder.DropTable(
                name: "Rarities");
        }
    }
}
