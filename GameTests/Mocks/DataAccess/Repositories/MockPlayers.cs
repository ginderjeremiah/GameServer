using GameCore.DataAccess;
using GameCore.Entities.PlayerAttributes;
using GameCore.Entities.Players;

namespace GameTests.Mocks.DataAccess.Repositories
{
    internal class MockPlayers : IPlayers
    {
        public Player? GetPlayerByUserName(string userName)
        {
            throw new NotImplementedException();
        }

        public void SavePlayer(Player player, List<PlayerAttribute> attributes)
        {
            throw new NotImplementedException();
        }
    }
}
