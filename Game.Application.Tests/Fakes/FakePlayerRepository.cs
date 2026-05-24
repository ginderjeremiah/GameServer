using Game.Abstractions.DataAccess;
using Game.Core.Players;
using Game.Core.Players.Inventories;

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

        /// <summary>Counter that hands out unique IDs for simulated AddInventoryItem calls.</summary>
        private int _nextInventoryItemId = 1;

        public void Seed(Player player) => _store[player.Id] = player;

        public Task<Player?> GetPlayer(int playerId)
            => Task.FromResult(_store.TryGetValue(playerId, out var p) ? p : null);

        public Task SavePlayer(Player player)
        {
            _store[player.Id] = player;
            SavePlayerCallCount++;
            return Task.CompletedTask;
        }

        public Task<int> AddInventoryItem(int playerId, int itemId, int slotNumber, int rating = 1)
            => Task.FromResult(_nextInventoryItemId++);

        public Task UpdateInventoryItemSlots(int playerId, IEnumerable<IInventoryUpdate> updates)
            => Task.CompletedTask;
    }
}
