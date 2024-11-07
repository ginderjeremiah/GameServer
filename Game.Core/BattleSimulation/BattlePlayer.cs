using Game.Core.Sessions;
using static Game.Core.EAttribute;

namespace Game.Core.BattleSimulation
{
    public class BattlePlayer : Battler
    {
        public override BattleAttributes Attributes { get; set; }
        public override double CurrentHealth { get; set; }
        public override List<BattleSkill> Skills { get; set; }
        public override int Level { get; set; }

        public BattlePlayer(Session session)
        {
            Attributes = new BattleAttributes(session.BattlerAttributes.Concat(session.GetInventoryAttributes()));
            CurrentHealth = Attributes[MaxHealth];
            Skills = session.GetSelectedSkills().Select(skill => new BattleSkill(skill)).ToList();
            Level = session.Player.Level;
        }
    }
}
