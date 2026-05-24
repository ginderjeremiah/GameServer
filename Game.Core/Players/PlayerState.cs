namespace Game.Core.Players
{
    /// <summary>
    /// Represents the transient state of a player's current session, including active battle data.
    /// </summary>
    public class PlayerState
    {
        public int PlayerId { get; set; }

        public DateTime EnemyCooldown { get; set; } = DateTime.UnixEpoch;

        public DateTime EarliestDefeat { get; set; } = DateTime.UnixEpoch;

        public bool Victory { get; set; }

        public int? ActiveEnemyId { get; set; }

        public int? ActiveEnemyLevel { get; set; }

        public uint? BattleSeed { get; set; }

        public void SetActiveBattle(int enemyId, int level, uint seed, DateTime earliestDefeat, bool victory)
        {
            ActiveEnemyId = enemyId;
            ActiveEnemyLevel = level;
            BattleSeed = seed;
            EarliestDefeat = earliestDefeat;
            Victory = victory;
        }

        public bool CanDefeatEnemy(DateTime now)
        {
            return Victory && ActiveEnemyId.HasValue && now >= EarliestDefeat;
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
            EarliestDefeat = DateTime.UnixEpoch;
            Victory = false;
        }
    }
}
