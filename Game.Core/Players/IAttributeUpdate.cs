namespace Game.Core.Players
{
    /// <summary>
    /// Represents an update to a player attribute.
    /// </summary>
    public interface IAttributeUpdate
    {
        /// <summary>
        /// The ID of the attribute to update.
        /// </summary>
        EAttribute Attribute { get; }

        /// <summary>
        /// The signed delta to apply to the attribute's allocated points (negative to refund).
        /// </summary>
        int Amount { get; }
    }
}
