using Game.Core.Attributes;

namespace Game.Core.Battle
{
    /// <summary>
    /// The shared rule for assembling a battler's skill set from its sources. Parity-critical: it runs on
    /// both the backend (snapshot reconstruction) and the frontend (live battle build), and the two must
    /// agree tick-for-tick.
    /// </summary>
    public static class BattleLoadout
    {
        /// <summary>
        /// The ordered, de-duplicated, weapon-gated ids of the skills a battler fights with: the
        /// <paramref name="selectedSkillIds"/> first (in their chosen order), then <paramref name="grantedSkillIds"/>
        /// — the skills granted by active sources (equipped items today, including the equipped weapon's
        /// signature; set bonuses later) in source order. De-duplicated by id with the first occurrence winning,
        /// so a granted skill that duplicates a selected skill — or another grant — is fielded once.
        /// <para>
        /// Each surviving id is then passed through the <b>weapon-match gate</b> (spike #1342): a skill whose
        /// type (<paramref name="resolveSkillType"/>) is a <em>weapon leaf</em> (<see cref="DamageTypes.IsWeaponLeaf"/>
        /// — Sword/Axe/Bow/Club/Dagger/Unarmed) is fielded <b>iff</b> it matches <paramref name="equippedWeaponType"/>;
        /// weapon-agnostic types (generic Physical, the elementals, DoT, caster types) are never gated. The
        /// resolver returns <c>null</c> for an id with no resolvable skill (e.g. an unauthored punch); such an id
        /// is dropped, so the gate degrades gracefully rather than fielding a phantom skill. Applied uniformly to
        /// selected and granted ids: a weapon's own signature matches by authoring, and a non-weapon item that
        /// granted a weapon-typed skill is dormant unless its weapon is held.
        /// </para>
        /// </summary>
        public static IEnumerable<int> OrderSkillIds(
            IEnumerable<int> selectedSkillIds, IEnumerable<int> grantedSkillIds,
            EDamageType equippedWeaponType, Func<int, EDamageType?> resolveSkillType)
        {
            return selectedSkillIds
                .Concat(grantedSkillIds)
                .Distinct()
                .Where(id => resolveSkillType(id) is EDamageType type && IsFielded(type, equippedWeaponType));
        }

        /// <summary>
        /// The weapon-match gate (spike #1342): whether a skill of leaf type <paramref name="skillType"/> is
        /// fielded while <paramref name="equippedWeaponType"/> is wielded. A weapon-leaf-typed skill is fielded
        /// only when it matches the equipped weapon type (exact leaf-match); a weapon-agnostic type is always
        /// fielded. The single pure predicate the frontend mirrors, computed once at battler assembly — never on
        /// the deterministic tick loop — so it adds no RNG-coupled parity surface.
        /// </summary>
        public static bool IsFielded(EDamageType skillType, EDamageType equippedWeaponType)
        {
            return !DamageTypes.IsWeaponLeaf(skillType) || skillType == equippedWeaponType;
        }
    }
}
