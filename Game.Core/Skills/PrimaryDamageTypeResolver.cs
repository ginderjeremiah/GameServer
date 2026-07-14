namespace Game.Core.Skills
{
    /// <summary>
    /// The shared "primary damage type" tie-break rule: the highest-weight portion wins, the first-authored
    /// portion wins a weight tie, and an empty split falls back to <see cref="EDamageType.Physical"/>. Generic
    /// over the three skill representations that each carry a portion split under different property names and
    /// weight numeric types (the domain <see cref="Skill.DamagePortions"/>, the read contract, and the
    /// persisted entity) so all three resolve through one implementation instead of drifting independently.
    /// Takes an indexable list (not <see cref="IEnumerable{T}"/>) so it walks portions by index without
    /// allocating an enumerator; most call sites (battler assembly, admin-save validation) already hold their
    /// split as a list, while the progression-graph check materializes one.
    /// </summary>
    public static class PrimaryDamageTypeResolver
    {
        public static EDamageType Resolve<TPortion, TWeight>(
            IReadOnlyList<TPortion> portions,
            Func<TPortion, TWeight> weightSelector,
            Func<TPortion, EDamageType> typeSelector)
            where TWeight : IComparable<TWeight>
        {
            if (portions.Count == 0)
            {
                return EDamageType.Physical;
            }

            var primary = portions[0];
            for (var i = 1; i < portions.Count; i++)
            {
                if (weightSelector(portions[i]).CompareTo(weightSelector(primary)) > 0)
                {
                    primary = portions[i];
                }
            }
            return typeSelector(primary);
        }
    }
}
