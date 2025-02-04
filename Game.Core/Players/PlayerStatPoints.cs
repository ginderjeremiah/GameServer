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
            var availablePoints = StatPointsGained - StatPointsUsed;
            var matchedAttributes = StatAllocations.Select(att => (att, upd: changedAttributes.FirstOrDefault(chg => chg.Attribute == att.Attribute)));
            var changedPoints = matchedAttributes.Sum(match => match.upd?.Amount ?? 0);
            if (availablePoints - changedPoints >= 0 && matchedAttributes.All(match => match.att.Amount + (match.upd?.Amount ?? 0) >= 0))
            {
                StatPointsUsed += availablePoints - changedPoints;
                foreach (var (att, upd) in matchedAttributes)
                {
                    if (upd is not null)
                    {
                        att.Amount += upd.Amount;
                    }
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
            return StatAllocations.Select(att => new AttributeModifier
            {
                Amount = att.Amount,
                Attribute = att.Attribute,
                Source = EAttributeModifierSource.PlayerStatPoints,
                Type = EModifierType.Additive,
            });
        }
    }
}
