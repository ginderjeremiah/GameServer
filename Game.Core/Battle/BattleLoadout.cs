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
        /// The ordered, de-duplicated ids of the skills a battler fights with: the
        /// <paramref name="selectedSkillIds"/> first (in their chosen order), then
        /// <paramref name="grantedSkillIds"/> — the skills granted by active sources (equipped items today;
        /// set bonuses later) in source order. De-duplicated by id with the first occurrence winning, so a
        /// granted skill that duplicates a selected skill — or another grant — is fielded once. Callers gather
        /// the granted ids from <em>all</em> active sources before passing them in, so a future grant source is
        /// an additive concat into <paramref name="grantedSkillIds"/>, not a rewrite of this rule (spike #980).
        /// </summary>
        public static IEnumerable<int> OrderSkillIds(
            IEnumerable<int> selectedSkillIds, IEnumerable<int> grantedSkillIds)
        {
            return selectedSkillIds.Concat(grantedSkillIds).Distinct();
        }
    }
}
