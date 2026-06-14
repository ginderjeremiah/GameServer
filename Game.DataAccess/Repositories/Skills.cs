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
        private IReadOnlyList<Skill> Entities => holder.Current.Entities;

        public List<Contracts.Skill> AllSkills()
        {
            return [.. Entities.Select(SkillMapper.ToContract)];
        }

        public Skill? LookupSkill(int skillId)
        {
            return Entities.Lookup(skillId);
        }

        public CoreSkill GetSkill(int skillId)
        {
            // Returns the snapshot's shared, pre-materialized instance rather than rebuilding a fresh graph
            // per call. The skill template is reference data treated as immutable by every caller — BattleSkill
            // only reads from it — so sharing is safe.
            return holder.Current.CoreSkills.GetById(skillId, "skill");
        }
    }
}
