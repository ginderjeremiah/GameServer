using GameCore.Entities;

namespace GameCore.DataAccess
{
    public interface IPlayers
    {
        public Task<Player?> GetPlayerByUserNameAsync(string userName);
        public Task SavePlayerAsync(Player player, List<PlayerAttribute> attributes);
    }
}
