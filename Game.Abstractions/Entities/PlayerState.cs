using Game.Core.Battle;

namespace Game.Abstractions.Entities
{
    public class PlayerState
    {
        public DateTime BattleStartTime { get; set; }
        public DateTime EnemyCooldown { get; set; }
        public BattleSnapshot? Snapshot { get; set; }
    }
}
