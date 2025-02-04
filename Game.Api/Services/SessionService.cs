using Game.Abstractions.DataAccess;
using Game.Core.Battle;
using Game.Core.Players;

namespace Game.Api.Services
{
    /// <summary>
    /// A service for loading <see cref="Session"/> data for a request.
    /// </summary>
    /// <param name="repos">The <see cref="IRepositoryManager"/> which is used to load session data.</param>
    public class SessionService(IRepositoryManager repos)
    {
        private readonly IRepositoryManager _repos = repos;
        private Player? _player;

        /// <summary>
        /// The id of the currently loaded session.
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// The id of the currently loaded user. 
        /// </summary>
        public int UserId { get; private set; }

        /// <summary>
        /// The id of the currently selected player.
        /// </summary>
        public int SelectedPlayerId => PlayerState.PlayerId;

        /// <inheritdoc cref="PlayerState"/>
        public PlayerState PlayerState { get; private set; } = new();

        /// <summary>
        /// Indicates whether session data has been loaded into the <see cref="SessionService"/> or not.
        /// </summary>
        public bool Authenticated => UserId > 0;

        /// <summary>
        /// Loads the <see cref="PlayerState"/> data for the given <paramref name="sessionId"/>.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public async Task LoadSession(int userId, string sessionId)
        {
            UserId = userId;
            var sessionData = await _repos.SessionStore.GetSession(sessionId);
            if (sessionData is not null)
            {
                PlayerState = sessionData;
            }
        }

        /// <summary>
        /// Loads the <see cref="Player"/> data for the currently selected player.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<Player> LoadPlayer()
        {
            return _player ??= await _repos.Players.GetPlayer(SelectedPlayerId) ?? throw new InvalidOperationException("Player data not loaded.");
        }

        /// <summary>
        /// Clears the current session data.
        /// </summary>
        public void ClearSession()
        {
            PlayerState = new();
            SelectedPlayerId = 0;
            UserId = 0;
            // TODO: Clear session data from repository
        }

        /// <summary>
        /// Sets the active enemy for the current player session.
        /// </summary>
        /// <param name="battleData"></param>
        /// <param name="enemyCooldown"></param>
        public void SetActiveBattleData(BattleData battleData, DateTime enemyCooldown)
        {
            PlayerState.EnemyCooldown = enemyCooldown;
            _repos.SessionStore.Update(PlayerState, SelectedPlayerId);
            _repos.SessionStore.SetBattleDataHash(SelectedPlayerId, battleData.Hash());
        }

        /// <summary>
        /// Validates the given <paramref name="battleData"/> against the current player session.
        /// </summary>
        /// <param name="battleData"></param>
        /// <returns></returns>
        public async Task<bool> ValidateBattleData(BattleData battleData)
        {
            var battleDataHash = await _repos.SessionStore.GetAndDeleteBattleDataHash(SelectedPlayerId);
            return battleData.Hash() == battleDataHash;
        }
    }
}
