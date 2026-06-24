using Game.Core.Attributes;

namespace Game.Core.Players
{
    /// <summary>
    /// Builds the <see cref="NewPlayer"/> blueprint for a freshly created account. The new-player
    /// defaults — the starter skill set, the base attribute spread, and the default log
    /// preferences — are game rules ("what does a new player look like?"), so they live in the
    /// domain rather than in the orchestration layer.
    /// </summary>
    public class NewPlayerFactory
    {
        /// <summary>Number of starter skills granted to a new player (skill ids 0..N-1), all selected.</summary>
        public const int StarterSkillCount = 3;

        /// <summary>The starting amount for each of a new player's core attributes.</summary>
        public const double StartingAttributeAmount = 5d;

        /// <summary>The zone a new player begins in.</summary>
        public const int StartingZoneId = 0;

        /// <summary>
        /// Creates the blueprint for a brand-new player with the given <paramref name="name"/>: the
        /// starter skills (all selected), the root-proficiency seed skills (unselected — a tree-seeded root
        /// with no world skill source grants its native skill so the root is trainable from creation; spike
        /// #982 area D), the base attribute spread, and the default log preferences. Root proficiencies are
        /// open by construction (their <c>StartsUnlocked</c> flag), so only the seed-skill grant is seeded
        /// here, not any per-player open state. <paramref name="rootSeedSkillIds"/> is resolved from the
        /// proficiency catalogue by the orchestration layer; it is empty until roots are authored.
        /// </summary>
        public NewPlayer Create(string name, IReadOnlyList<int> rootSeedSkillIds)
        {
            // Starter skills are selected (ids 0..N-1); root seed skills are appended unselected, dropping any
            // already covered by a starter skill so a player never gets a duplicate skill row.
            var starterSkills = Enumerable.Range(0, StarterSkillCount)
                .Select((id, index) => new NewPlayerSkill { SkillId = id, Selected = true, Order = index });
            var seedSkills = rootSeedSkillIds
                .Distinct()
                .Where(id => id >= StarterSkillCount)
                .Select((id, index) => new NewPlayerSkill
                {
                    SkillId = id,
                    Selected = false,
                    Order = StarterSkillCount + index,
                });

            return new NewPlayer
            {
                Name = name,
                Level = 1,
                Exp = 0,
                CurrentZoneId = StartingZoneId,
                StatPointsGained = 0,
                StatPointsUsed = 0,
                Skills = [.. starterSkills, .. seedSkills],
                // Seed an allocation row for exactly the core (directly-allocatable) attributes, derived from
                // the attribute set itself rather than a hardcoded count — so adding a seventh core attribute
                // automatically grants new players its allocation row (without one, PlayerStatPoints rejects
                // every allocation into it, permanently blocking the stat).
                Attributes = Enum.GetValues<EAttribute>()
                    .Where(Attribute.IsCore)
                    .Select(attribute => new StatAllocation { Attribute = attribute, Amount = StartingAttributeAmount })
                    .ToList(),
                LogPreferences = CreateDefaultLogPreferences(),
            };
        }

        private static List<LogPreference> CreateDefaultLogPreferences()
        {
            return
            [
                new() { LogType = ELogType.Damage, Enabled = false },
                new() { LogType = ELogType.Debug, Enabled = false },
                new() { LogType = ELogType.Exp, Enabled = true },
                new() { LogType = ELogType.LevelUp, Enabled = true },
                new() { LogType = ELogType.ItemFound, Enabled = true },
                new() { LogType = ELogType.EnemyDefeated, Enabled = true },
                new() { LogType = ELogType.SkillEffect, Enabled = true },
                new() { LogType = ELogType.Proficiency, Enabled = true },
            ];
        }
    }
}
