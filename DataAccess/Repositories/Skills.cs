using GameCore.DataAccess;
using GameCore.Entities;
using GameCore.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories
{
    internal class Skills : BaseRepository, ISkills
    {
        private static List<Skill>? _skillDataList;

        public Skills(IDatabaseService database) : base(database) { }

        public async Task<IEnumerable<Skill>> AllSkillsAsync()
        {
            return _skillDataList ??= await Database.Skills
                .AsNoTracking()
                .Include(s => s.SkillDamageMultipliers)
                .ToListAsync();
        }

        public async Task<Skill?> GetSkillAsync(int skillId)
        {
            var skills = (await AllSkillsAsync()).ToList();
            return skills.Count > skillId ? null : skills[skillId];
        }

        public Task SaveSkillsAsync(List<int> skillIds)
        {
            throw new NotImplementedException();
        }
    }
}
