using GameCore.DataAccess;
using GameCore.Entities;
using GameInfrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories
{
    internal class Skills : BaseRepository, ISkills
    {
        private static List<Skill>? _skillDataList;

        public Skills(GameContext database) : base(database) { }

        public List<Skill> AllSkills(bool refreshCache = false)
        {
            if (_skillDataList == null || refreshCache)
            {
                _skillDataList ??= [.. Database.Skills
                    .AsNoTracking()
                    .Include(s => s.SkillDamageMultipliers)];
            }
            return _skillDataList;
        }

        public Skill? GetSkill(int skillId)
        {
            var skills = AllSkills();
            return skills.Count <= skillId ? null : skills[skillId];
        }

        public Task SaveSkillsAsync(List<int> skillIds)
        {
            throw new NotImplementedException();
        }
    }
}
