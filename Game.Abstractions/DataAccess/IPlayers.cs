using Game.Core.Players;

namespace Game.Abstractions.DataAccess
{
    public interface IPlayers
    {
        public Task<Player?> GetPlayer(int playerId);
        public Task SavePlayer(Player player);
    }
}
