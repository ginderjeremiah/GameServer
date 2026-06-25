using Game.Core.Attributes;
using Game.Core.Classes;

namespace Game.Core.Players
{
    /// <summary>
    /// Builds the <see cref="NewPlayer"/> blueprint for a freshly created character from its chosen
    /// <see cref="Class"/>. "What does a new player look like?" is a game rule, so it lives in the domain
    /// rather than the orchestration layer; the class is the parameter that drives it — the starter kit
    /// and the starting attribute spread come from class data, replacing the former hardcoded constants.
    /// </summary>
    public class NewPlayerFactory
    {
        /// <summary>The zone a new player begins in.</summary>
        public const int StartingZoneId = 0;

        /// <summary>
        /// Creates the blueprint for a brand-new player with the given <paramref name="name"/> of the chosen
        /// <paramref name="class"/>: the class's starter skills (all selected, in authored order), the starting
        /// attribute allocation sourced from the class's attribute distribution base spread, and the default
        /// log preferences. The character's proficiency roots are no longer seeded here — they emerge from the
        /// kit, whose skills open their paths through derived openness on the first won battle (spike #1126).
        /// </summary>
        public NewPlayer Create(string name, Class @class)
        {
            // Starter skills come from the class kit, all selected, in authored order. Dedup by id (first wins)
            // so a kit that lists the same skill twice — e.g. the shared path-less "punch" already covered — never
            // produces a duplicate skill row.
            var seen = new HashSet<int>();
            var starterSkills = @class.StarterSkillIds
                .Where(seen.Add)
                .Select((id, index) => new NewPlayerSkill { SkillId = id, Selected = true, Order = index })
                .ToList();

            // The class's base attribute spread, keyed by attribute. This is the level-1 base only; the
            // level-scaled, non-reallocatable locked base and the reduced free pool are introduced in #1223.
            var baseByAttribute = @class.AttributeDistributions
                .ToDictionary(distribution => distribution.AttributeId, distribution => (double)distribution.BaseAmount);

            return new NewPlayer
            {
                ClassId = @class.Id,
                Name = name,
                Level = 1,
                Exp = 0,
                CurrentZoneId = StartingZoneId,
                StatPointsGained = 0,
                StatPointsUsed = 0,
                Skills = starterSkills,
                // Seed an allocation row for exactly the core (directly-allocatable) attributes, derived from the
                // attribute set itself rather than a hardcoded count — so adding a seventh core attribute
                // automatically grants new players its allocation row (without one, PlayerStatPoints rejects every
                // allocation into it, permanently blocking the stat). The amount is the class's base spread for
                // that attribute, or 0 for an attribute the class does not invest in.
                Attributes = Enum.GetValues<EAttribute>()
                    .Where(Attribute.IsCore)
                    .Select(attribute => new StatAllocation
                    {
                        Attribute = attribute,
                        Amount = baseByAttribute.TryGetValue(attribute, out var amount) ? amount : 0d,
                    })
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
