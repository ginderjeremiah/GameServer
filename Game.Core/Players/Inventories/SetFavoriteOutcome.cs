namespace Game.Core.Players.Inventories
{
    /// <summary>
    /// The result of a favorite toggle, distinguishing <b>rejected</b> from <b>accepted-but-unchanged</b> so
    /// the caller can answer the client and decide whether to persist independently — a same-value toggle is
    /// a legitimate request that simply mutated nothing, not a failure.
    /// </summary>
    public enum SetFavoriteOutcome
    {
        /// <summary>The player does not own the item: rejected as anti-cheat, nothing mutated.</summary>
        ItemNotUnlocked,

        /// <summary>The item is already in the requested state: accepted, but nothing mutated.</summary>
        Unchanged,

        /// <summary>The flag actually flipped, so the change needs an event and a save.</summary>
        Changed,
    }
}
