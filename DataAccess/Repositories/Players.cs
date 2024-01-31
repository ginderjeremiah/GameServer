using DataAccess.Models.Players;
using DataAccess.Models.SessionStore;
using DataAccess.Models.Stats;
using DataAccess.Redis;
using StackExchange.Redis;
using System.Data.SqlClient;

namespace DataAccess.Repositories
{
    internal class Players : BaseRepository, IPlayers
    {
        public static readonly object _lock = new();
        public static bool _processingQueue = false;

        public Players(string connectionString) : base(connectionString) { }

        [RedisSubscriber(Constants.REDIS_PLAYER_CHANNEL)]
        internal static void SubscriberCallback(RepositoryManager repos, RedisValue value)
        {
            if (!_processingQueue)
            {
                lock (_lock)
                {
                    _processingQueue = true;
                    while (repos.Redis.TryGetFromQueue<string>(Constants.REDIS_PLAYER_QUEUE, out var sessionId))
                    {
                        if (repos.Redis.TryGet<SessionData>($"{Constants.REDIS_SESSION_PREFIX}_{sessionId}", out var sessionData))
                        {
                            repos.Players.SavePlayer(sessionData.PlayerData, sessionData.Stats);
                        }
                    }
                    _processingQueue = false;
                }
            }
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

        public void SavePlayer(Player player, BaseStats stats)
        {
            var commandText = @"
                UPDATE Players
                SET Level = @Level,
                    Exp = @Exp,
                    StatPointsGained = @StatPointsGained,
                    StatPointsUsed = @StatPointsUsed
                WHERE PlayerId = @PlayerId
            
                UPDATE PlayerBaseStats
                SET Strength = @Strength,
                    Endurance = @Endurance,
                    Intellect = @Intellect,
                    Agility = @Agility,
                    Dexterity = @Dexterity,
                    Luck = @Luck
                WHERE PlayerId = @PlayerId";

            ExecuteNonQuery(commandText,
                new SqlParameter("@PlayerId", player.PlayerId),
                new SqlParameter("@Level", player.Level),
                new SqlParameter("@Exp", player.Exp),
                new SqlParameter("@StatPointsGained", player.StatPointsGained),
                new SqlParameter("@StatPointsUsed", player.StatPointsUsed),
                new SqlParameter("@Strength", stats.Strength),
                new SqlParameter("@Endurance", stats.Endurance),
                new SqlParameter("@Intellect", stats.Intellect),
                new SqlParameter("@Agility", stats.Agility),
                new SqlParameter("@Dexterity", stats.Dexterity),
                new SqlParameter("@Luck", stats.Luck)
            );
        }
    }

    public interface IPlayers
    {
        public Player? GetPlayerByUserName(string userName);
        public void SavePlayer(Player player, BaseStats stats);
    }
}
