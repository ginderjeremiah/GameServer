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

        /// <summary>
        /// Whether the active battle is a dedicated-boss challenge (started via <c>ChallengeBoss</c>) rather
        /// than a random idle encounter. Captured at battle start so the resolve path knows a victory is a
        /// dedicated-boss clear, instead of inferring it from the enemy's <c>IsBoss</c> flag.
        /// </summary>
        public bool IsBossBattle { get; set; }

        /// <summary>
        /// The id of the zone the active battle takes place in, captured at battle start. For a boss
        /// challenge this is the challenged zone (which need not equal the player's current zone), so the
        /// clear is recorded against the correct zone.
        /// </summary>
        public int? BattleZoneId { get; set; }

        /// <summary>
        /// The ids of the enemy's selected battle skills captured at battle start. Snapshotting the
        /// loadout (rather than re-rolling it at validation time) is what guarantees the server
        /// validates against the exact same skills the client received and simulated with.
        /// </summary>
        public List<int>? ActiveEnemySkillIds { get; set; }

        /// <summary>
        /// The RNG seed for the battle simulation, shared with the client so both sides' simulations
        /// stay in lock-step. It is reserved for the simulation's randomness and is deliberately not
        /// consumed by battle setup (level roll / skill selection), so the client and server begin
        /// the simulation from the identical RNG state.
        /// </summary>
        public uint? BattleSeed { get; set; }

        /// <summary>
        /// Snapshot of the player's battle-relevant state captured at battle start.
        /// Used to reconstruct the player's <see cref="Battler"/> at simulation time,
        /// ensuring mid-battle mutations don't affect the outcome.
        /// </summary>
        public BattleSnapshot? Snapshot { get; set; }

        public bool HasActiveBattle => ActiveEnemyId.HasValue;

        public void SetActiveBattle(
            int enemyId,
            int level,
            List<int> enemySkillIds,
            uint seed,
            DateTime startTime,
            BattleSnapshot snapshot,
            int zoneId,
            bool isBossBattle)
        {
            ActiveEnemyId = enemyId;
            ActiveEnemyLevel = level;
            ActiveEnemySkillIds = enemySkillIds;
            BattleSeed = seed;
            BattleStartTime = startTime;
            Snapshot = snapshot;
            BattleZoneId = zoneId;
            IsBossBattle = isBossBattle;
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
            ActiveEnemySkillIds = null;
            BattleSeed = null;
            BattleStartTime = DateTime.UnixEpoch;
            Snapshot = null;
            BattleZoneId = null;
            IsBossBattle = false;
        }
    }
}
