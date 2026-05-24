using Game.Abstractions.DataAccess;
using Game.Core.Players;

namespace Game.Application.Tests.Fakes
{
    /// <summary>
    /// In-memory fake for <see cref="IPlayerRepository"/> used in unit tests.
    /// </summary>
    internal class FakePlayerRepository : IPlayerRepository
    {
        private readonly Dictionary<int, Player> _store = [];

        /// <summary>Number of times <see cref="SavePlayer"/> was called.</summary>
        public int SavePlayerCallCount { get; private set; }

        public void Seed(Player player) => _store[player.Id] = player;

        public Task<Player?> GetPlayer(int playerId)
            => Task.FromResult(_store.TryGetValue(playerId, out var p) ? p : null);

        public Task SavePlayer(Player player)
        {
            _store[player.Id] = player;
            SavePlayerCallCount++;
            return Task.CompletedTask;
        }

        // Unlocked items
        public Task UnlockItem(int playerId, int itemId) => Task.CompletedTask;
        public Task EquipItem(int playerId, int itemId, int equipmentSlotId) => Task.CompletedTask;
        public Task UnequipItem(int playerId, int itemId) => Task.CompletedTask;

        // Unlocked mods
        public Task UnlockMod(int playerId, int itemModId) => Task.CompletedTask;

        // Applied mods
        public Task ApplyMod(int playerId, int itemId, int itemModSlotId, int itemModId) => Task.CompletedTask;
        public Task RemoveMod(int playerId, int itemId, int itemModSlotId) => Task.CompletedTask;

        // Statistics
        public Task<long> IncrementStatistic(int playerId, int statisticTypeId, int entityId, long amount)
            => Task.FromResult(amount);

        // Challenges
        public Task UpdateChallengeProgress(int playerId, int challengeId, int progress) => Task.CompletedTask;
        public Task CompleteChallenge(int playerId, int challengeId) => Task.CompletedTask;
    }
}
