using Game.Core.Entities;

namespace Game.Core.DataAccess
{
    public interface IPlayers
    {
        public Task<Player?> GetPlayer(string userName);
        public Task<Player?> GetPlayer(int playerId);
        public Task SavePlayer(Player player, bool playerDirty, bool inventoryDirty, bool skillsDirty);
        public Task<bool> CheckIfUsernameExists(string userName);
    }
}
