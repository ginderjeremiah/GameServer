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
        /// starter skills (all selected), the base attribute spread, and the default log preferences.
        /// </summary>
        public NewPlayer Create(string name)
        {
            return new NewPlayer
            {
                Name = name,
                Level = 1,
                Exp = 0,
                CurrentZoneId = StartingZoneId,
                StatPointsGained = 0,
                StatPointsUsed = 0,
                Skills = Enumerable.Range(0, StarterSkillCount)
                    .Select((id, index) => new NewPlayerSkill { SkillId = id, Selected = true, Order = index })
                    .ToList(),
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
            ];
        }
    }
}
