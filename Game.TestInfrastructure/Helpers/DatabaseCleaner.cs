using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Game.TestInfrastructure.Helpers
{
    public static class DatabaseCleaner
    {
        public static async Task TruncatePlayerDataAsync(GameContext context)
        {
            // Truncate player-related and game entity tables.
            // Preserves migration-seeded reference data: Attributes, EquipmentSlots,
            // ItemCategories, ItemModTypes, LogSettings, TagCategories.
            await context.Database.ExecuteSqlRawAsync("""
                TRUNCATE TABLE
                    "AppliedMods",
                    "UnlockedMods",
                    "UnlockedItems",
                    "PlayerAttributes",
                    "PlayerSkills",
                    "PlayerChallenges",
                    "PlayerStatistics",
                    "LogPreferences",
                    "UserLogins",
                    "Devices",
                    "BrowserInfos",
                    "Players",
                    "Users",
                    "ZoneEnemies",
                    "EnemySkills",
                    "AttributeDistributions",
                    "SkillDamageMultipliers",
                    "ItemAttributes",
                    "ItemModAttributes",
                    "ItemModSlots",
                    "Enemies",
                    "Items",
                    "ItemMods",
                    "Skills",
                    "Zones",
                    "Tags",
                    "Challenges",
                    "Paths",
                    "Proficiencies",
                    "ProficiencyLevelModifiers",
                    "ProficiencyLevelRewards",
                    "ProficiencyPrerequisites",
                    "SkillPathContributions"
                RESTART IDENTITY CASCADE
                """);
        }
    }
}
