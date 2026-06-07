using Game.Abstractions.DataAccess;
using Game.Abstractions.Entities;
using Game.DataAccess.Mapping;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using CoreSkill = Game.Core.Skills.Skill;

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

        public void InvalidateCache() => _skillDataList = null;

        public List<Skill> AllSkills(bool refreshCache = false)
        {
            if (_skillDataList is null || refreshCache)
            {
                _skillDataList = _context.Skills
                    .AsNoTracking()
                    .Include(s => s.SkillDamageMultipliers)
                    .OrderBy(s => s.Id)
                    .ToList();
            }
            return _skillDataList;
        }

        public Skill? LookupSkill(int skillId)
        {
            var skills = AllSkills();
            return skills.Count <= skillId || skillId < 0 ? null : skills[skillId];
        }

        public CoreSkill GetSkill(int skillId)
        {
            var skills = AllSkills();
            return SkillMapper.ToCore(skills[skillId]);
        }
    }
}
