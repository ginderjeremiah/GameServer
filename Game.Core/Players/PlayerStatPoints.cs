using CoreAttribute = Game.Core.Attributes.Attribute;

namespace Game.Core.Players
{
    /// <summary>
    /// Represents the stat points a player can and has allocated to core attributes.
    /// </summary>
    public class PlayerStatPoints
    {
        /// <summary>
        /// The attributes a player carries an allocation row for — the core (directly-allocatable) set,
        /// materialized in the enum's declaration order rather than read off the unordered
        /// <see cref="CoreAttribute.CoreAttributes"/> set, so <see cref="CreateAllocations"/> seeds every new
        /// character in the same deterministic order. Nothing reads meaning into the order of a player's
        /// stored allocations (see <see cref="Rehydrate"/>).
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
        /// The rows are a display/persistence convenience — they give the client a complete per-attribute
        /// spread to render and reconcile against without inferring the missing entries — not a correctness
        /// requirement: <see cref="TryUpdateAttributes"/> decides allocatability from
        /// <see cref="CoreAttribute.IsCore"/> and creates a row on first spend, so a missing one costs a
        /// stale display at worst. The amount is zero because the class's starting spread is delivered by
        /// the level-scaled locked base at battler assembly, never seeded into the free pool.
        /// </summary>
        public static List<StatAllocation> CreateAllocations()
        {
            return [.. AllocatableAttributes.Select(attribute => new StatAllocation { Attribute = attribute, Amount = 0d })];
        }

        /// <summary>
        /// Rebuilds a persisted player's stat points, restoring an allocation row for any core attribute the
        /// stored set is missing one for. Rehydration goes through here rather than an object initializer so
        /// the complete-spread shape <see cref="CreateAllocations"/> establishes at creation cannot be
        /// bypassed by a load path that forgets to re-establish it, and so an existing character gains a row
        /// for a core attribute added after they were created. The repair is belt-and-braces rather than a
        /// correctness requirement — since <see cref="TryUpdateAttributes"/> stopped treating row presence
        /// as permission, a missing row is a lost zero, not the permanent lockout of #2459.
        /// <para>
        /// Restored rows are appended, so a repaired character's allocations are not in the same order as a
        /// freshly created one's. Nothing depends on that order: the client renders its stat steppers from
        /// its own core-attribute list, and an allocation only reaches battle math as a per-attribute
        /// additive modifier.
        /// </para>
        /// </summary>
        public static PlayerStatPoints Rehydrate(List<StatAllocation> statAllocations, int statPointsGained, int statPointsUsed)
        {
            var allocated = statAllocations.Select(allocation => allocation.Attribute).ToHashSet();
            foreach (var attribute in AllocatableAttributes)
            {
                if (!allocated.Contains(attribute))
                {
                    statAllocations.Add(new StatAllocation { Attribute = attribute, Amount = 0d });
                }
            }

            return new PlayerStatPoints
            {
                StatAllocations = statAllocations,
                StatPointsGained = statPointsGained,
                StatPointsUsed = statPointsUsed,
            };
        }

        /// <summary>
        /// Attempts to apply the given <paramref name="changedAttributes"/> to the player's stat allocations.
        /// </summary>
        /// <param name="changedAttributes"></param>
        /// <returns><see langword="true"/> if successful, otherwise <see langword="false"/></returns>
        public bool TryUpdateAttributes(IEnumerable<IAttributeUpdate> changedAttributes)
        {
            var allocationsByAttribute = StatAllocations.ToDictionary(allocation => allocation.Attribute);
            // Match each update to the player's allocation row, creating one at zero if the attribute has
            // none yet. Two payloads are rejected outright (no mutation, all-or-nothing anti-cheat contract):
            //  - An update targeting a non-core attribute (#488): only the core attributes are directly
            //    allocatable, so allocating into a derived (or undefined) one is an invalid request, not a
            //    silent no-op that still reports success. The rule is asked of the attribute itself rather
            //    than inferred from row presence — a seeded row is a display convenience maintained
            //    elsewhere, and reading permission off it made a dropped row a permanent lockout (#2459).
            //  - A duplicate update for the same attribute (#698): an ambiguous payload can't be resolved
            //    to a single intended amount, so the whole request is invalid rather than a silent
            //    partial apply (keeping only the first update) that still reports success.
            var matchedUpdates = new Dictionary<EAttribute, (StatAllocation Allocation, IAttributeUpdate Update)>();
            var createdAllocations = new List<StatAllocation>();
            foreach (var update in changedAttributes)
            {
                if (!CoreAttribute.IsCore(update.Attribute))
                {
                    return false;
                }

                if (!allocationsByAttribute.TryGetValue(update.Attribute, out var allocation))
                {
                    allocation = new StatAllocation { Attribute = update.Attribute, Amount = 0d };
                    createdAllocations.Add(allocation);
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
                // Rows created above are published only now, so a rejected payload leaves the allocation set
                // exactly as it found it.
                StatAllocations.AddRange(createdAllocations);
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
