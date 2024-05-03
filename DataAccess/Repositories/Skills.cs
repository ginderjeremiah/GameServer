using DataAccess.Entities.Skills;
using GameLibrary.Database.Interfaces;
using StackExchange.Redis;

namespace DataAccess.Repositories
{
    internal class Skills : BaseRepository, ISkills
    {
        private static List<Skill>? _skillDataList;

        public Skills(IDataProvider database) : base(database) { }

        public List<Skill> AllSkills()
        {
            return _skillDataList ??= GetAllSkills();
        }

        public Skill GetSkill(int skillId)
        {
            return AllSkills()[skillId];
        }

        public void SaveSkills(List<int> skillIds)
        {
            throw new NotImplementedException();
        }

        private List<Skill> GetAllSkills()
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
                    AttributeId,
                    Multiplier
                FROM SkillDamageMultipliers";

            var result = Database.QueryToList<Skill, SkillDamageMultiplier>(commandText);

            var multipliers = result.Item2
                .GroupBy(m => m.SkillId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var skill in result.Item1)
            {
                skill.DamageMultipliers = multipliers[skill.SkillId];
            }

            return result.Item1;
        }
    }

    public interface ISkills
    {
        public List<Skill> AllSkills();
        public Skill GetSkill(int skillId);
        public void SaveSkills(List<int> skillIds);
    }
}
