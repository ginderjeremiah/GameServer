using Game.Core.Battle;
using Game.Core.Battle.Events;
using Game.Core.Enemies;
using Game.Core.Events;
using Game.Core.Players;
using Game.Core.Players.Inventories;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Game.Application.Tests.Events
{
    /// <summary>
    /// Unit coverage for <see cref="LoggingEventHandler"/>. The handler must never serialize the event
    /// itself — an event can carry a whole aggregate (e.g. the <see cref="Player"/> on
    /// <see cref="BattleCompletedEvent"/>), so default formatting would leak player identity, stats, and
    /// inventory into the logs. It logs the event type name always, plus a curated set of safe scalar ids
    /// only when the event opts in via <see cref="ILoggableDomainEvent"/>.
    /// </summary>
    public class LoggingEventHandlerTests
    {
        [Fact]
        public async Task NonLoggableEvent_LogsTypeNameOnly_WithNoExtraProperties()
        {
            var (handler, capturing) = CreateHandler();

            await handler.HandleAsync(new PlainEvent(), CancellationToken.None);

            var entry = Assert.Single(capturing.Entries);
            Assert.Equal(LogLevel.Debug, entry.Level);
            Assert.Equal($"Domain event dispatched: {nameof(PlainEvent)}", entry.Message);
            // No projection was supplied, so no curated-property scope was opened.
            Assert.Empty(entry.ScopeStates);
        }

        [Fact]
        public async Task LoggableEvent_LogsTypeName_AndCuratedScalarsAsStructuredProperties()
        {
            var (handler, capturing) = CreateHandler();
            var loggable = new LoggableEvent([new("PlayerId", 7), new("ZoneId", 9)]);

            await handler.HandleAsync(loggable, CancellationToken.None);

            var entry = Assert.Single(capturing.Entries);
            Assert.Equal($"Domain event dispatched: {nameof(LoggableEvent)}", entry.Message);

            var scoped = Assert.Single(entry.ScopeStates);
            var properties = Assert.IsAssignableFrom<IReadOnlyList<KeyValuePair<string, object?>>>(scoped)
                .ToDictionary(p => p.Key, p => p.Value);
            Assert.Equal(7, properties["PlayerId"]);
            Assert.Equal(9, properties["ZoneId"]);
        }

        [Fact]
        public async Task BattleCompletedEvent_LogsCuratedIds_AndNeverSerializesThePlayerAggregate()
        {
            var (handler, capturing) = CreateHandler();
            var player = MakePlayer(id: 7, name: "SecretPlayerName");
            var enemy = MakeEnemy(id: 5, name: "SecretEnemyName");
            var evt = new BattleCompletedEvent(
                player, enemy, Victory: true, PlayerDied: false, TotalMs: 3200,
                Stats: new BattleStats { PlayerDamageDealt = 1234.5 }, IsBossBattle: true, ZoneId: 9);

            await handler.HandleAsync(evt, CancellationToken.None);

            var entry = Assert.Single(capturing.Entries);
            // The message is the type name only — never the event's own ToString (which would print the
            // whole Player/Enemy aggregate).
            Assert.Equal($"Domain event dispatched: {nameof(BattleCompletedEvent)}", entry.Message);

            var properties = Assert.IsAssignableFrom<IReadOnlyList<KeyValuePair<string, object?>>>(
                Assert.Single(entry.ScopeStates)).ToDictionary(p => p.Key, p => p.Value);
            Assert.Equal(7, properties["PlayerId"]);
            Assert.Equal(5, properties["EnemyId"]);

            // The aggregate, its identity, and its stats appear nowhere in the captured log output.
            Assert.DoesNotContain(player, properties.Values);
            Assert.DoesNotContain(enemy, properties.Values);
            var captured = entry.Message + "|" + string.Join("|", properties.Select(p => $"{p.Key}={p.Value}"));
            Assert.DoesNotContain("SecretPlayerName", captured);
            Assert.DoesNotContain("SecretEnemyName", captured);
            Assert.DoesNotContain("1234.5", captured);
        }

        private static (LoggingEventHandler Handler, CapturingLoggerProvider Capturing) CreateHandler()
        {
            var capturing = new CapturingLoggerProvider();
            // The handler logs at Debug, so the factory must let Debug through to the capturing provider.
            var loggerFactory = LoggerFactory.Create(builder => builder
                .SetMinimumLevel(LogLevel.Debug)
                .AddProvider(capturing));
            return (new LoggingEventHandler(loggerFactory.CreateLogger<LoggingEventHandler>()), capturing);
        }

        private sealed record PlainEvent : IDomainEvent;

        private sealed record LoggableEvent(IReadOnlyList<KeyValuePair<string, object?>> Properties)
            : IDomainEvent, ILoggableDomainEvent
        {
            public IReadOnlyList<KeyValuePair<string, object?>> GetLogProperties() => Properties;
        }

        private static Player MakePlayer(int id, string name) => new()
        {
            Id = id,
            Name = name,
            Level = 1,
            Exp = 0,
            CurrentZoneId = 0,
            StatPoints = new PlayerStatPoints { StatAllocations = [], StatPointsGained = 0, StatPointsUsed = 0 },
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
