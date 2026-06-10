using Game.Abstractions.DataAccess;
using Game.DataAccess.Mapping;
using Game.DataAccess.Repositories.Caching;
using Game.Infrastructure.Entities;
using Contracts = Game.Abstractions.Contracts;
using CoreSkill = Game.Core.Skills.Skill;

namespace Game.DataAccess.Repositories
{
    internal class Skills(SkillsCacheHolder holder) : ISkills, ISkillEntityCache
    {
        private IReadOnlyList<Skill> Entities => holder.Current;

        public IReadOnlyList<Skill> AllSkillEntities()
        {
            return Entities;
        }

        public List<Contracts.Skill> AllSkills()
        {
            return [.. Entities.Select(SkillMapper.ToContract)];
        }

        public Skill? LookupSkill(int skillId)
        {
            var skills = Entities;
            return skills.Count <= skillId || skillId < 0 ? null : skills[skillId];
        }

        public CoreSkill GetSkill(int skillId)
        {
            return SkillMapper.ToCore(Entities[skillId]);
        }
    }
}
