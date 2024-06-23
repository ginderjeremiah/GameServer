using GameCore.DataAccess;
using GameCore.Entities.Skills;

namespace GameTests.Mocks.DataAccess.Repositories
{
    internal class MockSkills : ISkills
    {
        public List<Skill> Skills { get; set; } = new();
        public List<Skill> AllSkills()
        {
            return Skills;
        }

        public Skill GetSkill(int skillId)
        {
            return Skills.First(skill => skill.SkillId == skillId);
        }

        public void SaveSkills(List<int> skillIds)
        {
            throw new NotImplementedException();
        }
    }
}
