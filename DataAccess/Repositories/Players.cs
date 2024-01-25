using DataAccess.Models.Player;
using System.Data.SqlClient;

namespace DataAccess.Repositories
{
    internal class Players : BaseRepository, IPlayers
    {
        public Players(string connectionString) : base(connectionString) { }

        public Player? GetPlayerByUserName(string userName)
        {
            var commandText = @"
                SELECT TOP(1)
                    PlayerId,
                    PlayerName,
                    UserName,
                    Level,
                    Exp,
                    StatPointsGained,
                    StatPointsUsed,
                    Salt,
                    PassHash
                FROM Players AS P
                WHERE UserName = @UserName";

            return QueryToList<Player>(commandText, new SqlParameter("@UserName", userName)).FirstOrDefault();
        }
    }

    public interface IPlayers
    {
        public Player? GetPlayerByUserName(string userName);
    }
}
