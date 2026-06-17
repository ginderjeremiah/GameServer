namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// A process-stable version key for an intrinsic (enum-derived) reference-data command. Intrinsic sets
    /// have no cache holder to swap — their data is fixed for the process lifetime — so they cannot key the
    /// memoized version on a snapshot instance. Each command type instead reuses a single sentinel, so its
    /// version is computed once and then served from the memo for the life of the process, while staying
    /// distinct from every other set's key.
    /// </summary>
    /// <typeparam name="TCommand">The intrinsic command type, ensuring one distinct sentinel per set.</typeparam>
    internal static class IntrinsicVersionKey<TCommand>
    {
        /// <summary>The per-set sentinel; identical for every instance of <typeparamref name="TCommand"/>.</summary>
        public static object Instance { get; } = new();
    }
}
