using Game.Core.Attributes.Modifiers;

namespace Game.Core.Players
{
    /// <summary>
    /// Represents the stat points a player can and has allocated to core attributes.
    /// </summary>
    public class PlayerStatPoints
    {
        /// <summary>
        /// The number of stat points the player has gained from levels and other sources.
        /// </summary>
        public required int StatPointsGained { get; set; }

        /// <summary>
        /// The number of stat points the player has used from the amount gained.
        /// </summary>
        public required int StatPointsUsed { get; set; }

        /// <inheritdoc cref="StatAllocation"/>
        public List<StatAllocation> StatAllocations { get; set; }

        /// <summary>
        /// Creates a new instance of <see cref="PlayerStatPoints"/>.
        /// </summary>
        /// <param name="statAllocations"></param>
        public PlayerStatPoints(List<StatAllocation> statAllocations)
        {
            StatAllocations = statAllocations;
        }

        /// <summary>
        /// Attempts to apply the given <paramref name="changedAttributes"/> to the player's stat allocations.
        /// </summary>
        /// <param name="changedAttributes"></param>
        /// <returns><see langword="true"/> if successful, otherwise <see langword="false"/></returns>
        public bool TryUpdateAttributes(IEnumerable<IAttributeUpdate> changedAttributes)
        {
            var allocationsByAttribute = StatAllocations.ToDictionary(allocation => allocation.Attribute);
            // Match each update to the player's existing allocation row, keeping the first update per
            // attribute (matching the prior FirstOrDefault). An update targeting an attribute the player
            // has no allocation row for is rejected outright (#488): only the core attributes are seeded
            // as rows, so allocating into an unknown (or derived) attribute is an invalid request, not a
            // silent no-op that still reports success.
            var matchedUpdates = new Dictionary<EAttribute, (StatAllocation Allocation, IAttributeUpdate Update)>();
            foreach (var update in changedAttributes)
            {
                if (!allocationsByAttribute.TryGetValue(update.Attribute, out var allocation))
                {
                    return false;
                }

                matchedUpdates.TryAdd(update.Attribute, (allocation, update));
            }

            var changedPoints = matchedUpdates.Values.Sum(match => match.Update.Amount);
            var availablePoints = StatPointsGained - StatPointsUsed;
            if (availablePoints - changedPoints >= 0
                && matchedUpdates.Values.All(match => match.Allocation.Amount + match.Update.Amount >= 0))
            {
                StatPointsUsed += changedPoints;
                foreach (var (allocation, update) in matchedUpdates.Values)
                {
                    allocation.Amount += update.Amount;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Converts the stat allocations to a list of <see cref="AttributeModifier"/>.
        /// </summary>
        public IEnumerable<AttributeModifier> ToAttributeModifiers()
        {
            return StatAllocations.Select(allocation => allocation.ToModifier());
        }
    }
}
