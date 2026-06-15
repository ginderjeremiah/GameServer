namespace Game.Core.Events
{
    /// <summary>
    /// Opt-in projection that lets a domain event expose a deliberate, curated set of SAFE scalar
    /// identifiers for logging. The generic logging handler never serializes the event itself, so an
    /// event that carries an aggregate (e.g. the whole <c>Player</c>) only contributes the small,
    /// reviewed projection it returns here — never player identity, stats, or inventory by default.
    /// An event that does not implement this contributes only its type name, so newly-added events are
    /// safe by default and can never silently leak an aggregate into the logs.
    /// </summary>
    public interface ILoggableDomainEvent
    {
        /// <summary>
        /// The safe scalar identifiers to attach to the log entry as structured properties.
        /// Only include non-sensitive scalars (ids, counts, flags) — never aggregates or PII.
        /// </summary>
        IReadOnlyList<KeyValuePair<string, object?>> GetLogProperties();
    }
}
