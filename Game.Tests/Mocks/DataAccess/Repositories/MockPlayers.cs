using GameCore.DataAccess;
using GameCore.Entities;

namespace GameTests.Mocks.DataAccess.Repositories
{
    internal class MockPlayers : IPlayers
    {
        public List<Player> Players { get; set; } = new();
        public Player? GetPlayerByUserName(string userName)
        {
            return Players.FirstOrDefault(p => p.UserName == userName);
        }

        public void SavePlayer(Player player, List<PlayerAttribute> attributes)
        {
            throw new NotImplementedException();
        }
    }
}
