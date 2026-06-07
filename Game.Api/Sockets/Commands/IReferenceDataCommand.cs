namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Implemented by the reference-data socket commands (the <c>Get*</c> commands the
    /// loading screen pulls). Exposes the command name and a content version of its data
    /// so <see cref="GetReferenceDataVersions"/> can report, in one round-trip, which sets
    /// a client's cached copy is stale against — without duplicating the per-set source
    /// mapping each command already owns.
    /// </summary>
    public interface IReferenceDataCommand
    {
        /// <summary>The socket command name a client uses to fetch this set (e.g. <c>GetZones</c>).</summary>
        string Name { get; }

        /// <summary>
        /// A stable content hash of this set's current data. Identical for identical data and
        /// changes whenever the client-visible data changes.
        /// </summary>
        string ComputeVersion();
    }
}
