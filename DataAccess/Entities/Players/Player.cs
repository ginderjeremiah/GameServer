using GameLibrary;
using System.Data.SqlClient;

namespace DataAccess.Entities.Players
{
    public class Player : IEntity
    {
        public int PlayerId { get; set; }
        public string UserName { get; set; }
        public Guid Salt { get; set; }
        public string PassHash { get; set; }
        public string PlayerName { get; set; }
        public int Level { get; set; }
        public int Exp { get; set; }
        public int StatPointsGained { get; set; }
        public int StatPointsUsed { get; set; }

        public void LoadFromReader(SqlDataReader reader)
        {
            PlayerId = reader["PlayerId"].AsInt();
            UserName = reader["UserName"].AsString();
            Salt = new Guid(reader["Salt"].AsString());
            PassHash = reader["PassHash"].AsString();
            PlayerName = reader["PlayerName"].AsString();
            Level = reader["Level"].AsInt();
            Exp = reader["Exp"].AsInt();
            StatPointsGained = reader["StatPointsGained"].AsInt();
            StatPointsUsed = reader["StatPointsUsed"].AsInt();
        }
    }
}
