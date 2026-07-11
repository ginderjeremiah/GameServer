namespace Game.Abstractions.DataAccess
{
    /// <summary>
    /// Thrown by <see cref="IPlayerRepository.SavePlayer"/> (and by a standalone <c>IPlayerProgressRepository.Save</c>
    /// outside a player save) when the write-behind queue flush fails, or when the domain-event dispatch that feeds
    /// it faults (a transient publish or handler error — not a cancellation, which propagates as
    /// <see cref="OperationCanceledException"/> unwrapped). The buffered domain events stay queued for a same-scope
    /// retry, but that scope ends with the command, so this distinct type lets the socket layer recognize the
    /// failure and force the connection's in-memory player to reload from the last successfully-persisted state,
    /// rather than silently carrying this save's mutations forward with no corresponding queued event (#1632, #1819).
    /// </summary>
    public class PlayerPersistenceFlushFailedException(Exception innerException)
        : Exception("Failed to flush the player persistence batch to the write-behind queue.", innerException)
    {
    }
}
