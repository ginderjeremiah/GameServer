namespace Game.DataAccess.PlayerUpdates
{
    /// <summary>
    /// Persists a single dequeued player write-behind event to the database. There is one handler per
    /// persisted event type, resolved per drain scope by <see cref="PlayerUpdateEventDispatcher"/>.
    /// Implementations must be idempotent (existence checks, absolute updates, delete-then-insert) because
    /// the queue read is at-least-once, so a reclaimed or retried event can be applied more than once.
    /// </summary>
    /// <remarks>
    /// No <c>CancellationToken</c> by design: a reserved item is applied without the shutdown token so an
    /// in-flight save completes rather than being cancelled and then dead-lettered, and a wedged save is
    /// already bounded by Npgsql's command timeout plus the retry path (#1029, docs/backend-persistence.md).
    /// </remarks>
    internal interface IPlayerUpdateHandler<TEvent>
    {
        Task HandleAsync(TEvent updateEvent);
    }
}
