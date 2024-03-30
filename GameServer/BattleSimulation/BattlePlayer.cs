using DataAccess.Models.Skills;
using GameServer.Auth;
using static DataAccess.Attributes;

namespace GameServer.BattleSimulation
{
    public class BattlePlayer : Battler
    {
        public override BattleAttributes Attributes { get; set; }
        public override double CurrentHealth { get; set; }
        public override List<BattleSkill> Skills { get; set; }
        public override int Level { get; set; }

        public BattlePlayer(SessionPlayer playerData, List<Skill> skills)
        {
            Attributes = new BattleAttributes(playerData.Attributes);
            CurrentHealth = Attributes[MaxHealth];
            Skills = playerData.SelectedSkills.Select(id => new BattleSkill(skills[id])).ToList();
            Level = playerData.Level;
        }
    }
}
