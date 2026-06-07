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
        /// <summary>The maximum number of skills an enemy brings into a battle.</summary>
        private const int MaxBattleSkills = 4;

        private List<Skill>? _battleSkills;

        public required int Id { get; init; }
        public required string Name { get; init; }
        public required int Level { get; init; }
        public required bool IsBoss { get; init; }
        public required List<AttributeDistribution> AttributeDistributions { get; init; }
        public required List<Skill> AvailableSkills { get; init; }

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
        /// Randomly selects this enemy's loadout for a new encounter: up to <see cref="MaxBattleSkills"/>
        /// skills drawn from its available skills. The chosen loadout is the source of truth — it is
        /// snapshotted and sent to the client — so the selection deliberately uses ambient randomness
        /// rather than the battle seed, keeping that seed reserved as the battle simulation's RNG source
        /// (identical on client and server).
        /// </summary>
        public void SelectBattleSkills()
        {
            _battleSkills = [.. AvailableSkills.OrderBy(_ => Random.Shared.Next()).Take(MaxBattleSkills)];
        }

        /// <summary>
        /// Restores a previously-selected battle loadout from its <paramref name="skillIds"/> (in order),
        /// resolved against this enemy's available skills. Used to reconstruct the exact same encounter
        /// when a battle's result is validated server-side.
        /// </summary>
        public void SetBattleSkills(IReadOnlyList<int> skillIds)
        {
            var available = AvailableSkills.ToDictionary(skill => skill.Id);
            _battleSkills = [.. skillIds.Select(id => available[id])];
        }
    }
}
