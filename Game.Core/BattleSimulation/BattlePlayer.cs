using Game.Core.Sessions;
using static Game.Core.EAttribute;

namespace Game.Core.BattleSimulation
{
    /// <summary>
    /// An extension of the <see cref="Battler"/> class to be create from a player <see cref="Session"/>.
    /// </summary>
    public class BattlePlayer : Battler
    {
        /// <inheritdoc/>
        public override BattleAttributes Attributes { get; set; }

        /// <inheritdoc/>
        public override double CurrentHealth { get; set; }

        /// <inheritdoc/>
        public override List<BattleSkill> Skills { get; set; }

        /// <inheritdoc/>
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
