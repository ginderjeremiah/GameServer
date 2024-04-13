using DataAccess.Entities.PlayerAttributes;
using DataAccess.Entities.Players;
using DataAccess.Redis;
using Microsoft.SqlServer.Server;
using StackExchange.Redis;
using System.Data;
using System.Data.SqlClient;

namespace DataAccess.Repositories
{
    internal class Players : BaseRepository, IPlayers
    {
        public static readonly object _lock = new();
        public static bool _processingQueue = false;

        public Players(string connectionString) : base(connectionString) { }

        [RedisSubscriber(Constants.REDIS_PLAYER_CHANNEL, Constants.REDIS_PLAYER_QUEUE)]
        internal static void ProcessPlayerUpdate(RepositoryManager repos, RedisValue queueValue)
        {
            if (repos.SessionStore.TryGetSession(queueValue, out var sessionData))
                repos.Players.SavePlayer(sessionData.PlayerData, sessionData.Attributes);
        }

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

        public void SavePlayer(Player player, List<PlayerAttribute> attributes)
        {
            var data = new SqlMetaData[2];
            data[0] = new SqlMetaData("AttributeId", SqlDbType.Int);
            data[1] = new SqlMetaData("Amount", SqlDbType.Decimal);

            var records = attributes.Select(att =>
            {
                var record = new SqlDataRecord(data);
                record.SetInt32(0, att.AttributeId);
                record.SetDecimal(1, att.Amount);
                return record;
            }).ToArray();

            if (records.Length == 0)
            {
                records = null;
            }

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

            ExecuteNonQuery(commandText,
                new SqlParameter("@PlayerId", player.PlayerId),
                new SqlParameter("@Level", player.Level),
                new SqlParameter("@Exp", player.Exp),
                new SqlParameter("@StatPointsGained", player.StatPointsGained),
                new SqlParameter("@StatPointsUsed", player.StatPointsUsed),
                new SqlParameter("@Attributes", SqlDbType.Structured)
                {
                    Value = records,
                    TypeName = "AttributeUpdate"
                }
            );
        }
    }

    public interface IPlayers
    {
        public Player? GetPlayerByUserName(string userName);
        public void SavePlayer(Player player, List<PlayerAttribute> attributes);
    }
}
