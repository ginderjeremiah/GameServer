using GameCore;
using GameCore.Database.Interfaces;
using System.Data;

namespace DataAccess.Entities.Skills
{
    public class SkillDamageMultiplier : IEntity
    {
        public int SkillId { get; set; }
        public int AttributeId { get; set; }
        public decimal Multiplier { get; set; }

        public void LoadFromReader(IDataRecord record)
        {
            SkillId = record["SkillId"].AsInt();
            AttributeId = record["AttributeId"].AsInt();
            Multiplier = record["Multiplier"].AsDecimal();
        }
    }
}
