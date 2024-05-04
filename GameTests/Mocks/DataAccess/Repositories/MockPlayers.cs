using DataAccess.Entities.PlayerAttributes;
using DataAccess.Entities.Players;
using DataAccess.Repositories;

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
