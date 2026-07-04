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
                    -- Classes are referenced by Players (not the reverse), so no truncate above cascades to
                    -- them; listed explicitly so class content doesn't leak across tests. Their child tables
                    -- (ClassStarterSkills/ClassAttributeDistributions/ClassStarterEquipment) cascade from here.
                    "Classes",
                    "Challenges",
                    "Paths",
                    "Proficiencies",
                    "ProficiencyLevelModifiers",
                    "ProficiencyLevelRewards",
                    "ProficiencyPrerequisites",
                    -- Lessons carry no FK to any table above (ScreenKey/TriggerMechanicEvent are plain values,
                    -- not references), so nothing cascades into them; listed explicitly so lesson content
                    -- doesn't leak across tests. LessonSteps cascades from here.
                    "Lessons"
                RESTART IDENTITY CASCADE
                """);
        }
    }
}
