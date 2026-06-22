using Game.Core.Battle;
using Game.Core.Players;
using Game.Core.Skills;

namespace Game.Core.TestInfrastructure.Builders
{
    /// <summary>
    /// Test-only convenience for building a <see cref="Battler"/> straight from a live <see cref="Player"/>
    /// aggregate. Production never does this: it reconstructs a player's battler from a frozen
    /// <see cref="BattleSnapshot"/> (the anti-cheat replay surface). This shortcut keeps battle tests terse
    /// by composing the same attributes/skills/level off the live aggregate, and the two construction paths
    /// are pinned to agree by BattleSnapshotTests.AssertBattlerParity.
    /// </summary>
    public static class BattlerFactory
    {
        /// <summary>
        /// Builds a battler from the live player. Selected skills come straight off the aggregate; the
        /// skills granted by equipped items (<see cref="Items.Item.GrantedSkillId"/>) are resolved against
        /// the supplied <paramref name="resolveSkill"/> — the live item carries only the id, mirroring the
        /// snapshot — and the two sets are ordered and de-duplicated by the shared
        /// <see cref="BattleLoadout.OrderSkillIds"/> rule so this matches <see cref="BattleSnapshot.ToBattler"/>.
        /// A resolver is required only when an equipped item actually grants a skill.
        /// </summary>
        public static Battler FromPlayer(Player player, Func<int, Skill>? resolveSkill = null)
        {
            var grantedSkillIds = player.Inventory.EquipmentSlots
                .SelectNotNull(slot => slot.Item?.GrantedSkillId)
                .ToList();

            if (grantedSkillIds.Count == 0)
            {
                return new Battler(player.GetAttributes(), player.SelectedSkills, player.Level);
            }

            if (resolveSkill is null)
            {
                throw new InvalidOperationException(
                    "A skill resolver is required to build a battler from a player whose equipped items grant skills.");
            }

            var selectedById = player.SelectedSkills.ToDictionary(skill => skill.Id);
            var skills = BattleLoadout
                .OrderSkillIds(player.SelectedSkills.Select(skill => skill.Id), grantedSkillIds)
                .Select(id => selectedById.TryGetValue(id, out var selected) ? selected : resolveSkill(id));

            return new Battler(player.GetAttributes(), skills, player.Level);
        }
    }
}
