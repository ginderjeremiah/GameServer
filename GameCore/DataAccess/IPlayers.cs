using GameCore.Entities.PlayerAttributes;
using GameCore.Entities.Players;

namespace GameCore.DataAccess
{
    public interface IPlayers
    {
        public Player? GetPlayerByUserName(string userName);
        public void SavePlayer(Player player, List<PlayerAttribute> attributes);
    }
}
