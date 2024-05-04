using GameCore;
using GameCore.Database.Interfaces;
using System.Data;

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

        public void LoadFromReader(IDataRecord record)
        {
            PlayerId = record["PlayerId"].AsInt();
            UserName = record["UserName"].AsString();
            Salt = new Guid(record["Salt"].AsString());
            PassHash = record["PassHash"].AsString();
            PlayerName = record["PlayerName"].AsString();
            Level = record["Level"].AsInt();
            Exp = record["Exp"].AsInt();
            StatPointsGained = record["StatPointsGained"].AsInt();
            StatPointsUsed = record["StatPointsUsed"].AsInt();
        }
    }
}
