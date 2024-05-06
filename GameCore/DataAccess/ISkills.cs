using GameCore.Entities.Skills;

namespace GameCore.DataAccess
{
    public interface ISkills
    {
        public List<Skill> AllSkills();
        public Skill GetSkill(int skillId);
        public void SaveSkills(List<int> skillIds);
    }
}
