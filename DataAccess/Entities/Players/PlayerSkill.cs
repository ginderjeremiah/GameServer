using GameLibrary;
using System.Data.SqlClient;

namespace DataAccess.Entities.Players
{
    public class PlayerSkill : IEntity
    {
        public int PlayerId { get; set; }
        public int SkillId { get; set; }
        public bool Selected { get; set; }

        public void LoadFromReader(SqlDataReader reader)
        {
            PlayerId = reader["PlayerId"].AsInt();
            SkillId = reader["SkillId"].AsInt();
            Selected = reader["Selected"].AsBool();
        }
    }
}
