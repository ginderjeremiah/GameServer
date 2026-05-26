using Game.Abstractions.DataAccess;
using Game.Core;
using Game.Core.Events;

namespace Game.Application
{
    public class BattleStatisticsEventHandler(
        IPlayerStatistics playerStatistics)
        : IDomainEventHandler<BattleCompletedEvent>
    {
        private readonly IPlayerStatistics _playerStatistics = playerStatistics;

        public async Task HandleAsync(BattleCompletedEvent e, CancellationToken cancellationToken = default)
        {
            var playerId = e.PlayerId;

            await _playerStatistics.IncrementStatistic(
                playerId, (int)EStatisticType.TotalDamageDealt, null, e.Stats.PlayerDamageDealt);

            await _playerStatistics.SetMaxStatistic(
                playerId, (int)EStatisticType.HighestSingleAttackDamage, null, e.Stats.HighestPlayerAttack);

            await _playerStatistics.IncrementStatistic(
                playerId, (int)EStatisticType.TotalDamageTaken, null, e.Stats.PlayerDamageTaken);

            await _playerStatistics.IncrementStatistic(
                playerId, (int)EStatisticType.EnemiesEncountered, null, 1);

            await _playerStatistics.IncrementStatistic(
                playerId, (int)EStatisticType.EnemiesEncountered, e.EnemyId, 1);

            if (e.Victory)
            {
                await _playerStatistics.IncrementStatistic(
                    playerId, (int)EStatisticType.BattlesWon, null, 1);

                await _playerStatistics.SetMinStatistic(
                    playerId, (int)EStatisticType.FastestVictoryMs, null, e.TotalMs);

                await _playerStatistics.SetMinStatistic(
                    playerId, (int)EStatisticType.FastestVictoryMs, e.EnemyId, e.TotalMs);
            }
            else
            {
                await _playerStatistics.IncrementStatistic(
                    playerId, (int)EStatisticType.BattlesLost, null, 1);
            }

            if (e.PlayerDied)
            {
                await _playerStatistics.IncrementStatistic(
                    playerId, (int)EStatisticType.PlayerDeaths, null, 1);
            }

            await _playerStatistics.IncrementStatistic(
                playerId, (int)EStatisticType.TotalBattleTimeMs, null, e.TotalMs);

            await _playerStatistics.IncrementStatistic(
                playerId, (int)EStatisticType.TotalSkillsUsed, null, e.Stats.PlayerSkillsUsed);
        }
    }
}
