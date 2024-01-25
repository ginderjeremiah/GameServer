using DataAccess.Models.Attributes;
using DataAccess.Models.Skills;
using GameLibrary;
using System.Data;

namespace DataAccess.Repositories
{
    internal class Skills : BaseRepository, ISkills
    {
        public Skills(string connectionString) : base(connectionString) { }

        public List<Skill> AllSkills()
        {
            var commandText = @"
                SELECT
                    SkillId,
                    SkillName,
                    CooldownMS,
                    SkillDesc,
                    BaseDamage,
                    IconPath
                FROM Skills
                ORDER BY SkillId

                SELECT
                    SkillId,
                    AttributeName,
                    Multiplier
                FROM SkillDamageMultipliers
                INNER JOIN Attributes
                ON SkillDamageMultipliers.AttributeId = Attributes.AttributeId";

            var ds = FillSet(commandText);
            var multipliers = ds.Tables[1].AsEnumerable()
                .GroupBy(row => row["SkillId"].AsInt())
                .ToDictionary(g => g.Key, g => g.Select(row => row.To<AttributeMultiplier>()).ToList());

            return ds.Tables[0].
                AsEnumerable()
                .Select(row => new Skill(row, multipliers[row["SkillId"].AsInt()]))
                .ToList();
        }
    }

    public interface ISkills
    {
        public List<Skill> AllSkills();
    }
}
