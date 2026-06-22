namespace Game.Core.Proficiencies
{
    /// <summary>
    /// The fixed-pie proficiency-XP split for a won battle (spike #982 decision 4). A constant pie, scaled by
    /// the battle's difficulty multiplier, is divided across the proficiencies <em>represented</em> in the
    /// fight — each weighted by <c>Σ(skillTierWeight × contributionWeight)</c> over the contributing skills
    /// that fired. Pure and reference-data-free: the caller resolves which skills fired and their weighted
    /// contributions, so the math here is the testable core (pie scaling + proportional split).
    /// </summary>
    public static class ProficiencyXpCalculator
    {
        /// <summary>
        /// One contributing skill's pull on a proficiency: the proficiency it feeds and the weight that
        /// contribution carries this battle (already <c>skillTierWeight × contributionWeight</c>). A single
        /// fired skill yields one of these per proficiency it contributes to; multiple fired skills feeding
        /// the same proficiency yield several, which sum into that proficiency's slice weight.
        /// </summary>
        public readonly record struct WeightedContribution(int ProficiencyId, double Weight);

        /// <summary>A proficiency's share of the battle's pie.</summary>
        public readonly record struct ProficiencyXpSlice(int ProficiencyId, double Xp);

        /// <summary>
        /// Splits the battle's pie across the represented proficiencies. The total is
        /// <paramref name="fixedPie"/> × <paramref name="difficultyMultiplier"/>; each proficiency's slice is
        /// proportional to its summed contribution weight. Returns slices ascending by proficiency id (a
        /// stable order so the live and offline paths, and their tests, agree). Empty when nothing is
        /// represented or the total weight is non-positive — no proficiency is trained.
        /// </summary>
        public static IReadOnlyList<ProficiencyXpSlice> Split(
            double fixedPie, double difficultyMultiplier, IEnumerable<WeightedContribution> contributions)
        {
            var weightByProficiency = new Dictionary<int, double>();
            foreach (var contribution in contributions)
            {
                weightByProficiency[contribution.ProficiencyId] =
                    weightByProficiency.GetValueOrDefault(contribution.ProficiencyId) + contribution.Weight;
            }

            var totalWeight = weightByProficiency.Values.Sum();
            if (totalWeight <= 0)
            {
                return [];
            }

            var total = fixedPie * difficultyMultiplier;
            return [.. weightByProficiency
                .OrderBy(pair => pair.Key)
                .Select(pair => new ProficiencyXpSlice(pair.Key, total * pair.Value / totalWeight))];
        }
    }
}
