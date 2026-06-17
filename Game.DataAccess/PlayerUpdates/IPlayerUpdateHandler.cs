namespace Game.DataAccess.PlayerUpdates
{
    /// <summary>
    /// Persists a single dequeued player write-behind event to the database. There is one handler per
    /// persisted event type, resolved per drain scope by <see cref="PlayerUpdateEventDispatcher"/>.
    /// Implementations must be idempotent (existence checks, absolute updates, delete-then-insert) because
    /// the queue read is at-least-once, so a reclaimed or retried event can be applied more than once.
    /// </summary>
    internal interface IPlayerUpdateHandler<TEvent>
    {
        Task HandleAsync(TEvent updateEvent);
    }
}
