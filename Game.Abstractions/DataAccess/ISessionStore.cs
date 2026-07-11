using Game.Core.Players;

namespace Game.Abstractions.DataAccess
{
    // Sessions are keyed by the user/account id (not PlayerState.PlayerId, which is a distinct value).
    public interface ISessionStore
    {
        public Task<PlayerState?> GetSession(int userId, CancellationToken cancellationToken = default);
        public void Update(PlayerState sessionData, int userId);
        /// <summary>
        /// Same write as <see cref="Update"/>, but awaited rather than fire-and-forget — for callers where a
        /// dropped write has a concrete, player-visible consequence rather than a self-healing one (the
        /// battle-lifecycle session save; see <c>SessionService.SavePlayerStateAsync</c>).
        /// </summary>
        public Task UpdateAsync(PlayerState sessionData, int userId, CancellationToken cancellationToken = default);
        public void Clear(int userId);
    }
}
