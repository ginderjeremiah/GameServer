namespace Game.Abstractions.DataAccess
{
    /// <summary>
    /// Broadcasts a "reference data changed" notification to every API instance after a successful admin
    /// write. Each instance subscribes at startup and reacts to another instance's notification with a
    /// debounced background reload of its in-memory reference caches (build-then-swap, so readers are never
    /// blocked); the publishing instance skips its own notification because the admin cache-reload filter
    /// has already reloaded its caches synchronously.
    /// </summary>
    public interface IReferenceDataChangeNotifier
    {
        /// <summary>
        /// Publishes the notification. The publish itself is awaited (a genuine send failure throws rather than
        /// vanishing silently), but delivery to subscribers remains Redis pub/sub's ordinary at-most-once
        /// guarantee — a subscriber mid-reconnect can still miss the message (#1888).
        /// </summary>
        Task NotifyChangedAsync();
    }
}
