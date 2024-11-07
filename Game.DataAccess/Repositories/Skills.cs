using Game.Core.DataAccess;
using Game.Core.Entities;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.Repositories
{
    internal class Skills : ISkills
    {
        private static List<Skill>? _skillDataList;

        private readonly GameContext _context;

        public Skills(GameContext context)
        {
            _context = context;
        }

        public List<Skill> AllSkills(bool refreshCache = false)
        {
            if (_skillDataList == null || refreshCache)
            {
                _skillDataList ??= [.. _context.Skills
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
