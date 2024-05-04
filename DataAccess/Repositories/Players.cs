using DataAccess.Entities.PlayerAttributes;
using DataAccess.Entities.Players;
using GameCore.Database;
using GameCore.Database.Interfaces;
using System.Data;

namespace DataAccess.Repositories
{
    internal class Players : BaseRepository, IPlayers
    {
        public static readonly object _lock = new();
        public static bool _processingQueue = false;

        public Players(IDataProvider database) : base(database) { }

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

            return Database.QueryToList<Player>(commandText, new QueryParameter("@UserName", userName)).FirstOrDefault();
        }

        public void SavePlayer(Player player, List<PlayerAttribute> attributes)
        {
            var structuredParameter = new StructuredQueryParameter("@Attributes", "AttributeUpdate");

            structuredParameter.AddColumns(
                ("AttributeId", DbType.Int32),
                ("Amount", DbType.Decimal)
            );

            structuredParameter.AddRows(attributes.Select(att => new List<object?>
            {
                att.AttributeId,
                att.Amount
            }).ToList());

            var commandText = @"
                UPDATE Players
                SET Level = @Level,
                    Exp = @Exp,
                    StatPointsGained = @StatPointsGained,
                    StatPointsUsed = @StatPointsUsed
                WHERE PlayerId = @PlayerId
            
                UPDATE PA
                SET Amount = A.Amount
                FROM PlayerAttributes PA
                INNER JOIN @Attributes A
                ON PA.AttributeId = A.AttributeId
                WHERE PlayerId = @PlayerId

                DELETE PA
                FROM PlayerAttributes PA
                LEFT JOIN @Attributes A
                ON PA.AttributeId = A.AttributeId
                WHERE A.AttributeId IS NULL";

            Database.ExecuteNonQuery(commandText,
                new QueryParameter("@PlayerId", player.PlayerId),
                new QueryParameter("@Level", player.Level),
                new QueryParameter("@Exp", player.Exp),
                new QueryParameter("@StatPointsGained", player.StatPointsGained),
                new QueryParameter("@StatPointsUsed", player.StatPointsUsed),
                structuredParameter
            );
        }
    }

    public interface IPlayers
    {
        public Player? GetPlayerByUserName(string userName);
        public void SavePlayer(Player player, List<PlayerAttribute> attributes);
    }
}
