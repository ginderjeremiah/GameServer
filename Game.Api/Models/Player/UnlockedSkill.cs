namespace Game.Api.Models.Player
{
    /// <summary>
    /// A skill the player has unlocked, with its loadout state. Parallel to
    /// <see cref="InventoryItems.InventoryItem"/>: <see cref="Selected"/> mirrors the item's
    /// <c>Equipped</c> flag and <see cref="Order"/> mirrors its nullable equipment-slot id (null
    /// when the skill is not equipped). The client derives the ordered equipped loadout from this
    /// richer set rather than receiving a separate equipped-id list.
    /// </summary>
    public class UnlockedSkill : IModel
    {
        public int SkillId { get; set; }

        public bool Selected { get; set; }

        /// <summary>The skill's zero-based position in the equipped loadout, or null when not selected.</summary>
        public int? Order { get; set; }
    }
}
