using GameCore.Entities;

namespace GameCore.DataAccess
{
    public interface IPlayers
    {
        public Task<Player?> GetPlayerByUserNameAsync(string userName);
        public Task<Player?> GetPlayerAsync(int playerId);
        public Task SavePlayerAsync(Player player, bool playerDirty, bool inventoryDirty, bool skillsDirty);
    }
}
