namespace Game.Core.Players
{
    /// <summary>
    /// The result of a stat-point allocation update, distinguishing <b>rejected</b> from
    /// <b>accepted-but-unchanged</b> so the caller can answer the client and decide whether to persist
    /// independently — a payload that allocates nothing is a legitimate request that simply mutated nothing,
    /// not a failure.
    /// </summary>
    public enum UpdateAttributesOutcome
    {
        /// <summary>
        /// The payload broke an anti-cheat rule (a non-core attribute, a duplicate attribute, a spend beyond
        /// the available points, or one driving an allocation below zero): nothing mutated.
        /// </summary>
        Rejected,

        /// <summary>
        /// The payload allocated nothing — it was empty, or every delta was zero: accepted, but nothing
        /// mutated.
        /// </summary>
        Unchanged,

        /// <summary>The allocations actually moved, so the change needs an event and a save.</summary>
        Changed,
    }
}
