using Game.Core.Players;

namespace Game.Abstractions.DataAccess
{
    /// <summary>
    /// Repository for the Player aggregate. Uses write-behind caching:
    /// reads go through Redis, writes update Redis immediately and queue
    /// async DB sync via domain events.
    /// </summary>
    public interface IPlayerRepository
    {
        Task<Player?> GetPlayer(int playerId, CancellationToken cancellationToken = default);
        Task SavePlayer(Player player, CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens a scope that any <see cref="IPlayerProgressRepository.Save"/> reached while it's active joins
        /// instead of flushing on its own — the same sharing <see cref="SavePlayer"/> gives a progress save
        /// reached through its own domain-event dispatch, exposed here for a caller that makes an explicit
        /// progress save alongside a separate <see cref="SavePlayer"/> call, so both share one flush and
        /// succeed or fail together rather than one durably committing while the other is left stranded.
        /// <see cref="SavePlayer"/> opens its own nested scope internally whose disposal ends the shared window,
        /// so any progress save meant to join it must run *before* the <see cref="SavePlayer"/> call, not after.
        /// </summary>
        IDisposable BeginBatch();
    }
}
