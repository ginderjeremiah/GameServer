using Game.Core.Battle;

namespace Game.Core.Players
{
    public class PlayerState
    {
        public int PlayerId { get; set; }

        public DateTime EnemyCooldown { get; set; } = DateTime.UnixEpoch;

        public DateTime BattleStartTime { get; set; } = DateTime.UnixEpoch;

        public int? ActiveEnemyId { get; set; }

        public int? ActiveEnemyLevel { get; set; }

        public uint? BattleSeed { get; set; }

        /// <summary>
        /// Snapshot of the player's battle-relevant state captured at battle start.
        /// Used to reconstruct the player's <see cref="Battler"/> at simulation time,
        /// ensuring mid-battle mutations don't affect the outcome.
        /// </summary>
        public BattleSnapshot? Snapshot { get; set; }

        public bool HasActiveBattle => ActiveEnemyId.HasValue;

        public void SetActiveBattle(int enemyId, int level, uint seed, DateTime startTime, BattleSnapshot snapshot)
        {
            ActiveEnemyId = enemyId;
            ActiveEnemyLevel = level;
            BattleSeed = seed;
            BattleStartTime = startTime;
            Snapshot = snapshot;
        }

        public void SetCooldown(DateTime cooldownUntil)
        {
            EnemyCooldown = cooldownUntil;
        }

        public bool IsOnCooldown(DateTime now)
        {
            return EnemyCooldown > now;
        }

        public void ClearBattle()
        {
            ActiveEnemyId = null;
            ActiveEnemyLevel = null;
            BattleSeed = null;
            BattleStartTime = DateTime.UnixEpoch;
            Snapshot = null;
        }
    }
}
