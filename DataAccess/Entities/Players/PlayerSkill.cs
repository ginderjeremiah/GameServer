using GameLibrary;
using GameLibrary.Database.Interfaces;
using System.Data;

namespace DataAccess.Entities.Players
{
    public class PlayerSkill : IEntity
    {
        public int PlayerId { get; set; }
        public int SkillId { get; set; }
        public bool Selected { get; set; }

        public void LoadFromReader(IDataRecord record)
        {
            PlayerId = record["PlayerId"].AsInt();
            SkillId = record["SkillId"].AsInt();
            Selected = record["Selected"].AsBool();
        }
    }
}
