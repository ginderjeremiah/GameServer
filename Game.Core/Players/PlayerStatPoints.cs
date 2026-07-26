using CoreAttribute = Game.Core.Attributes.Attribute;

namespace Game.Core.Players
{
    /// <summary>
    /// Represents the stat points a player can and has allocated to core attributes.
    /// </summary>
    public class PlayerStatPoints
    {
        /// <summary>
        /// The attributes a player carries an allocation row for — the core (directly-allocatable) set, in
        /// the enum's declaration order so every player's allocations read in the same canonical order.
        /// </summary>
        private static readonly EAttribute[] AllocatableAttributes =
            [.. Enum.GetValues<EAttribute>().Where(CoreAttribute.IsCore)];

        /// <summary>
        /// The number of stat points the player has gained from levels and other sources.
        /// </summary>
        public required int StatPointsGained { get; set; }

        /// <summary>
        /// The number of stat points the player has used from the amount gained.
        /// </summary>
        public required int StatPointsUsed { get; set; }

        /// <inheritdoc cref="StatAllocation"/>
        public required List<StatAllocation> StatAllocations { get; set; }

        /// <summary>
        /// Builds the seeded allocation set every player carries: one zero-amount row per core attribute.
        /// The rows must exist even at zero because <see cref="TryUpdateAttributes"/> rejects an allocation
        /// into an attribute with no row (the #488 anti-cheat), so a missing row permanently blocks its stat.
        /// The amount is zero because the class's starting spread is delivered by the level-scaled locked
        /// base at battler assembly, never seeded into the free pool.
        /// </summary>
        public static List<StatAllocation> CreateAllocations()
        {
            return [.. AllocatableAttributes.Select(attribute => new StatAllocation { Attribute = attribute, Amount = 0d })];
        }

        /// <summary>
        /// Restores a zero-amount row for every core attribute the player is missing one for, leaving
        /// existing rows (and their amounts) untouched. Applied when the aggregate is rehydrated from
        /// persistence so the seeded-row invariant <see cref="CreateAllocations"/> establishes at creation
        /// holds for the life of the character — self-healing a player whose zero rows were dropped by the
        /// pre-fix write-behind handler (#2459), and granting existing characters a row for a core attribute
        /// added after they were created.
        /// </summary>
        public void EnsureAllocatableAttributesArePresent()
        {
            var allocated = StatAllocations.Select(allocation => allocation.Attribute).ToHashSet();
            foreach (var attribute in AllocatableAttributes)
            {
                if (!allocated.Contains(attribute))
                {
                    StatAllocations.Add(new StatAllocation { Attribute = attribute, Amount = 0d });
                }
            }
        }

        /// <summary>
        /// Attempts to apply the given <paramref name="changedAttributes"/> to the player's stat allocations.
        /// </summary>
        /// <param name="changedAttributes"></param>
        /// <returns><see langword="true"/> if successful, otherwise <see langword="false"/></returns>
        public bool TryUpdateAttributes(IEnumerable<IAttributeUpdate> changedAttributes)
        {
            var allocationsByAttribute = StatAllocations.ToDictionary(allocation => allocation.Attribute);
            // Match each update to the player's existing allocation row. Two payloads are rejected
            // outright (no mutation, all-or-nothing anti-cheat contract):
            //  - An update targeting an attribute the player has no allocation row for (#488): only the
            //    core attributes are seeded as rows, so allocating into an unknown (or derived) attribute
            //    is an invalid request, not a silent no-op that still reports success.
            //  - A duplicate update for the same attribute (#698): an ambiguous payload can't be resolved
            //    to a single intended amount, so the whole request is invalid rather than a silent
            //    partial apply (keeping only the first update) that still reports success.
            var matchedUpdates = new Dictionary<EAttribute, (StatAllocation Allocation, IAttributeUpdate Update)>();
            foreach (var update in changedAttributes)
            {
                if (!allocationsByAttribute.TryGetValue(update.Attribute, out var allocation))
                {
                    return false;
                }

                if (!matchedUpdates.TryAdd(update.Attribute, (allocation, update)))
                {
                    return false;
                }
            }

            // long accumulator: matchedUpdates.Values.Sum for int uses checked arithmetic and would
            // throw OverflowException on a crafted payload (e.g. two near-int.MaxValue updates)
            // instead of honoring the all-or-nothing reject contract.
            var changedPoints = matchedUpdates.Values.Sum(match => (long)match.Update.Amount);
            var availablePoints = (long)StatPointsGained - StatPointsUsed;
            if (availablePoints - changedPoints >= 0
                && matchedUpdates.Values.All(match => match.Allocation.Amount + (long)match.Update.Amount >= 0))
            {
                StatPointsUsed += (int)changedPoints;
                foreach (var (allocation, update) in matchedUpdates.Values)
                {
                    allocation.Amount += update.Amount;
                }

                return true;
            }

            return false;
        }
    }
}
