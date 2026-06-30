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
        /// snapshot — and the two sets are ordered, de-duplicated, and weapon-gated by the shared
        /// <see cref="BattleLoadout.OrderSkillIds"/> rule so this matches <see cref="BattleSnapshot.ToBattler"/>.
        /// A resolver is required only when an equipped item actually grants a skill; the bare-hands punch
        /// (spike #1342) is resolver-gated, so the resolver-less shortcut a weaponless test battler uses fields
        /// no phantom punch and the gate is otherwise a no-op over weapon-agnostic skills.
        /// </summary>
        public static Battler FromPlayer(Player player, Func<int, Skill?>? resolveSkill = null)
        {
            var equippedItems = player.Inventory.EquipmentSlots.SelectNotNull(slot => slot.Item).ToList();
            var weapon = equippedItems.FirstOrDefault(item => item.Category == EItemCategory.Weapon);
            var equippedWeaponType = weapon?.WeaponType ?? EDamageType.Unarmed;

            var grantedSkillIds = equippedItems.SelectNotNull(item => item.GrantedSkillId).ToList();
            // Mirror the snapshot's virtual-fists punch (BattleSnapshot.GetBattleSkillIds): with no weapon
            // equipped the weapon slot's signature is punch. Resolver-gated so the shortcut weaponless battler
            // (no resolver) doesn't field it.
            if (weapon is null && resolveSkill is not null)
            {
                grantedSkillIds.Add(GameConstants.PunchSkillId);
            }

            if (grantedSkillIds.Count > 0 && resolveSkill is null)
            {
                throw new InvalidOperationException(
                    "A skill resolver is required to build a battler from a player whose equipped items grant skills.");
            }

            var selectedById = player.SelectedSkills.ToDictionary(skill => skill.Id);
            EDamageType? ResolveSkillType(int id) =>
                selectedById.TryGetValue(id, out var selected)
                    ? selected.PrimaryDamageType
                    : resolveSkill?.Invoke(id)?.PrimaryDamageType;

            var skills = BattleLoadout
                .OrderSkillIds(player.SelectedSkills.Select(skill => skill.Id), grantedSkillIds, equippedWeaponType, ResolveSkillType)
                .Select(id => selectedById.TryGetValue(id, out var selected)
                    ? selected
                    : resolveSkill?.Invoke(id) ?? throw new InvalidOperationException($"Skill {id} could not be resolved."));

            return new Battler(player.GetAttributes(), skills, player.Level);
        }
    }
}
