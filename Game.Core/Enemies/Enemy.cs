using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Skills;

namespace Game.Core.Enemies
{
    /// <summary>
    /// Represents an enemy that the player can encounter in the game.
    /// </summary>
    public class Enemy
    {
        private List<Skill>? _battleSkills;

        public required int Id { get; init; }
        public required string Name { get; init; }
        public required int Level { get; init; }
        public required bool IsBoss { get; init; }
        public required IReadOnlyList<AttributeDistribution> AttributeDistributions { get; init; }
        public required IReadOnlyList<Skill> AvailableSkills { get; init; }

        /// <summary>
        /// The battle loadout selected for an encounter. Only available after
        /// <see cref="SelectBattleSkills"/> or <see cref="SetBattleSkills"/> has been called.
        /// </summary>
        public IReadOnlyList<Skill> BattleSkills => _battleSkills
            ?? throw new InvalidOperationException(
                $"Battle skills have not been selected. Call {nameof(SelectBattleSkills)} or {nameof(SetBattleSkills)} first.");

        public IEnumerable<AttributeModifier> GetAttributeModifiers()
        {
            return AttributeDistributions.Select(d => d.GetDistributionModifier(Level));
        }

        /// <summary>
        /// Randomly selects this enemy's loadout for a new encounter: up to
        /// <see cref="GameConstants.MaxSelectedSkills"/> skills drawn from its available skills (the same
        /// cap the player's loadout uses, for player/enemy symmetry). The chosen loadout is the source of
        /// truth — it is snapshotted and sent to the client — so the selection deliberately uses ambient
        /// randomness rather than the battle seed, keeping that seed reserved as the battle simulation's
        /// RNG source (identical on client and server).
        /// </summary>
        public void SelectBattleSkills()
        {
            // Partial Fisher–Yates: draw an unbiased, uniformly-distributed sample (and ordering) of the
            // available skills, rather than the biased OrderBy(random) sort it replaces.
            var pool = AvailableSkills.ToArray();
            var take = Math.Min(GameConstants.MaxSelectedSkills, pool.Length);
            for (var i = 0; i < take; i++)
            {
                var swap = Random.Shared.Next(i, pool.Length);
                (pool[i], pool[swap]) = (pool[swap], pool[i]);
            }

            _battleSkills = [.. pool[..take]];
        }

        /// <summary>
        /// Selects this enemy's <em>full</em> authored loadout — every available skill, in authored order —
        /// for a deterministic encounter (the dedicated-boss challenge). Unlike <see cref="SelectBattleSkills"/>
        /// this neither caps the count at <see cref="GameConstants.MaxSelectedSkills"/> nor shuffles, so the loadout is fixed.
        /// The chosen loadout is the source of truth — it is snapshotted and sent to the client — so both
        /// sides simulate the boss with the identical skill set.
        /// </summary>
        public void SelectAllBattleSkills()
        {
            _battleSkills = [.. AvailableSkills];
        }

        /// <summary>
        /// Restores a previously-selected battle loadout from its <paramref name="skillIds"/> (in order),
        /// resolved against this enemy's available skills. Used to reconstruct the exact same encounter
        /// when a battle's result is validated server-side.
        /// </summary>
        public void SetBattleSkills(IReadOnlyList<int> skillIds)
        {
            var available = AvailableSkills.ToDictionary(skill => skill.Id);
            _battleSkills = [.. skillIds.Select(id => available.TryGetValue(id, out var skill)
                ? skill
                : throw new InvalidOperationException(
                    $"Cannot restore battle skill {id}: it is not among enemy {Id}'s available skills."))];
        }
    }
}
