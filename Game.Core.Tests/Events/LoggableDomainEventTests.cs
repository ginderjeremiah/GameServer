using Game.Core.Battle;
using Game.Core.Battle.Events;
using Game.Core.Enemies;
using Game.Core.Players;
using Game.Core.Players.Events;
using Game.Core.Players.Inventories;
using Xunit;

namespace Game.Core.Tests.Events
{
    /// <summary>
    /// Pins the curated log projections of the aggregate-carrying domain events. The projection is the
    /// only thing the logging handler may emit for these events, so it must expose safe scalar ids only —
    /// never the <see cref="Player"/> or <see cref="Enemy"/> aggregate (identity, stats, inventory).
    /// </summary>
    public class LoggableDomainEventTests
    {
        [Fact]
        public void BattleCompletedEvent_GetLogProperties_ReturnsOnlyCuratedSafeScalars()
        {
            var player = MakePlayer(id: 7, name: "SecretPlayerName");
            var enemy = MakeEnemy(id: 5, name: "SecretEnemyName");
            var stats = new BattleStats { PlayerDamageDealt = 1234.5 };
            var evt = new BattleCompletedEvent(
                player, enemy, Victory: true, PlayerDied: false, TotalMs: 3200,
                Stats: stats, IsBossBattle: true, ZoneId: 9);

            var properties = evt.GetLogProperties().ToDictionary(p => p.Key, p => p.Value);

            Assert.Equal(player.Id, Assert.IsType<int>(properties["PlayerId"]));
            Assert.Equal(enemy.Id, Assert.IsType<int>(properties["EnemyId"]));
            Assert.True(Assert.IsType<bool>(properties["Victory"]));
            Assert.False(Assert.IsType<bool>(properties["PlayerDied"]));
            Assert.True(Assert.IsType<bool>(properties["IsBossBattle"]));
            Assert.Equal(9, Assert.IsType<int>(properties["ZoneId"]));

            // The aggregates and their sensitive members are never projected — only the curated scalars.
            Assert.DoesNotContain(player, properties.Values);
            Assert.DoesNotContain(enemy, properties.Values);
            Assert.DoesNotContain(stats, properties.Values);
            var serialized = string.Join("|", properties.Select(p => $"{p.Key}={p.Value}"));
            Assert.DoesNotContain("SecretPlayerName", serialized);
            Assert.DoesNotContain("SecretEnemyName", serialized);
            Assert.DoesNotContain("1234.5", serialized);
        }

        [Fact]
        public void PlayerLeveledUpEvent_GetLogProperties_ReturnsOnlyCuratedSafeScalars()
        {
            var player = MakePlayer(id: 7, name: "SecretPlayerName");
            var evt = new PlayerLeveledUpEvent(player, NewLevel: 12, StatPointsGained: 60);

            var properties = evt.GetLogProperties().ToDictionary(p => p.Key, p => p.Value);

            Assert.Equal(player.Id, Assert.IsType<int>(properties["PlayerId"]));
            Assert.Equal(12, Assert.IsType<int>(properties["NewLevel"]));
            Assert.Equal(60, Assert.IsType<int>(properties["StatPointsGained"]));

            Assert.DoesNotContain(player, properties.Values);
            var serialized = string.Join("|", properties.Select(p => $"{p.Key}={p.Value}"));
            Assert.DoesNotContain("SecretPlayerName", serialized);
        }

        private static Player MakePlayer(int id, string name) => new()
        {
            Id = id,
            Name = name,
            Level = 1,
            Exp = 0,
            CurrentZoneId = 0,
            StatPoints = new PlayerStatPoints([]) { StatPointsGained = 0, StatPointsUsed = 0 },
            Inventory = new Inventory(),
            SelectedSkills = [],
            Skills = [],
            LogPreferences = [],
        };

        private static Enemy MakeEnemy(int id, string name) => new()
        {
            Id = id,
            Name = name,
            Level = 1,
            IsBoss = false,
            AttributeDistributions = [],
            AvailableSkills = [],
        };
    }
}
