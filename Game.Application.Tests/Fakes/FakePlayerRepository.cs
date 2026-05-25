using Game.Abstractions.DataAccess;
using Game.Core.Players;

namespace Game.Application.Tests.Fakes
{
    internal class FakePlayerRepository : IPlayerRepository
    {
        private readonly Dictionary<int, Player> _store = [];

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
    }
}
