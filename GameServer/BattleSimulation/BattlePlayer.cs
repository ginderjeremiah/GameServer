using DataAccess.Models.Skills;
using GameServer.Auth;

namespace GameServer.BattleSimulation
{
    public class BattlePlayer : Battler
    {
        public override BattleBaseStats Stats { get; set; }
        public override DerivedStats DerivedStats { get; set; }
        public override double CurrentHealth { get; set; }
        public override List<BattleSkill> Skills { get; set; }
        public override int Level { get; set; }

        public BattlePlayer(SessionPlayer playerData, List<Skill> skills)
        {
            Stats = playerData.Stats;
            DerivedStats = new DerivedStats(Stats);
            CurrentHealth = DerivedStats.MaxHealth;
            Skills = playerData.SelectedSkills.Select(id => new BattleSkill(skills[id])).ToList();
            Level = playerData.Level;
        }
    }
}
