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
        /// <paramref name="class"/>: the class's starter skills (all selected, in authored order), an empty
        /// free pool of stat allocations, and the default log preferences. The starting attribute spread is the
        /// class's level-scaled locked base, derived from <c>(class, level)</c> at battler assembly and never
        /// stored (spike #1126 area D) — so the free pool the player allocates starts at zero, not seeded with
        /// the base spread (which the locked base would then double-count). The character's proficiency roots
        /// are not seeded here either — they emerge from the kit, whose skills open their paths through derived
        /// openness on the first won battle (spike #1126).
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

            // The class's starting equipment — unlocked and equipped at creation. The slot↔category match is
            // an authoring-time guard (AdminClasses.SetStarterEquipment), so the kit is trusted well-formed
            // here, mirroring how the starter skills trust their authored Player-acquirable flag. The weapon's
            // innate skill (Item.GrantedSkillId) comes online through the normal equip path at battle assembly.
            var starterEquipment = @class.StarterEquipment
                .Select(equipment => new NewPlayerEquipment
                {
                    ItemId = equipment.ItemId,
                    EquipmentSlot = equipment.EquipmentSlot,
                })
                .ToList();

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
                Equipment = starterEquipment,
                // The seeded allocation rows are owned by PlayerStatPoints — the same set the rehydration path
                // re-establishes — so creation and reload can never disagree on the spread a player carries.
                Attributes = PlayerStatPoints.CreateAllocations(),
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
