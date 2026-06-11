using Game.Abstractions.Infrastructure;
using Game.Core;
using Game.Core.Events;
using Game.Core.Players.Events;
using Game.DataAccess;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Verifies that <see cref="DataProviderSynchronizer"/> no longer silently swallows or drops events while draining
    /// the player update queue: malformed payloads are dead-lettered, an unexpected failure is retried with backoff and
    /// dead-lettered only once the retries are exhausted, a transient failure that later succeeds is persisted, and in
    /// every case the failing message does not stop the remaining queued events from being processed.
    /// </summary>
    [Collection("Integration")]
    public class DataProviderSynchronizerTests : ApplicationIntegrationTestBase
    {
        // A zero-delay policy keeps the retry loop fast in tests while still exercising the full attempt count.
        private static readonly PlayerUpdateRetryPolicy TestRetryPolicy = new(maxAttempts: 3, baseDelay: TimeSpan.Zero);

        public DataProviderSynchronizerTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        [Fact]
        public async Task ProcessQueue_MalformedEventBeforeValidEvent_DeadLettersItAndStillPersistsValidEvent()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, level: 5, zoneId: 0);

            var validEvent = new PlayerCoreUpdatedEvent(
                PlayerId: player.Id,
                Level: 9,
                Exp: 1234,
                CurrentZoneId: 0,
                StatPointsGained: 100,
                StatPointsUsed: 100);

            var logger = new CapturingLogger<DataProviderSynchronizer>();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var synchronizer = new DataProviderSynchronizer(scope.ServiceProvider, pubsub, logger, TestRetryPolicy);

            // The malformed message is dequeued first; the synchronizer must dead-letter it and still apply the valid one.
            var queue = new InMemoryPubSubQueue("this is not a valid envelope", Serialize(validEvent));

            await synchronizer.ProcessQueue(queue);

            // The malformed payload is surfaced as a warning rather than silently swallowed.
            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);

            // The malformed message lands on the dead-letter queue rather than being dropped.
            var deadLettered = await DrainDeadLetterQueue(pubsub);
            Assert.Equal(["this is not a valid envelope"], deadLettered);

            // The whole queue was drained, and the valid event after the malformed one was still persisted.
            Assert.Null(await queue.GetNextAsync());

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var persisted = await verifyContext.Players.FindAsync([player.Id], CancellationToken);
            Assert.NotNull(persisted);
            Assert.Equal(9, persisted.Level);
            Assert.Equal(1234, persisted.Exp);
        }

        [Fact]
        public async Task ProcessQueue_UnexpectedFailureDuringHandling_RetriesThenDeadLettersAndContinues()
        {
            // An empty provider has no GameContext registered, so HandleEvent throws InvalidOperationException —
            // standing in for an unexpected failure (e.g. a database error) that persists across every retry.
            var brokenServices = new ServiceCollection().BuildServiceProvider();

            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var logger = new CapturingLogger<DataProviderSynchronizer>();
            var synchronizer = new DataProviderSynchronizer(brokenServices, pubsub, logger, TestRetryPolicy);

            var firstEvent = new PlayerCoreUpdatedEvent(1, 2, 3, 0, 100, 100);
            var secondEvent = new PlayerCoreUpdatedEvent(2, 3, 4, 0, 100, 100);
            var queue = new InMemoryPubSubQueue(Serialize(firstEvent), Serialize(secondEvent));

            await synchronizer.ProcessQueue(queue);

            // Each event is attempted MaxAttempts times: the non-final attempts log a "retrying" warning and the
            // final attempt logs an error before dead-lettering, and the first failure did not stop the second.
            Assert.Equal((TestRetryPolicy.MaxAttempts - 1) * 2, logger.Entries.Count(e => e.Level == LogLevel.Warning));
            Assert.Equal(2, logger.Entries.Count(e => e.Level == LogLevel.Error));
            Assert.Null(await queue.GetNextAsync());

            // Both events that exhausted their retries are preserved on the dead-letter queue rather than dropped.
            var deadLettered = await DrainDeadLetterQueue(pubsub);
            Assert.Equal(2, deadLettered.Count);
        }

        [Fact]
        public async Task ProcessQueue_TransientFailureThatLaterSucceeds_RetriesAndPersistsWithoutDeadLettering()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, level: 5, zoneId: 0);

            var validEvent = new PlayerCoreUpdatedEvent(
                PlayerId: player.Id,
                Level: 12,
                Exp: 4321,
                CurrentZoneId: 0,
                StatPointsGained: 100,
                StatPointsUsed: 100);

            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var logger = new CapturingLogger<DataProviderSynchronizer>();

            // The first scope creation throws (a simulated transient failure); the retry creates a real scope and persists.
            var flakyServices = new FlakyServiceProvider(scope.ServiceProvider, failuresBeforeSuccess: 1);
            var synchronizer = new DataProviderSynchronizer(flakyServices, pubsub, logger, TestRetryPolicy);

            var queue = new InMemoryPubSubQueue(Serialize(validEvent));

            await synchronizer.ProcessQueue(queue);

            // The transient failure was retried (one warning) and ultimately succeeded (no error, nothing dead-lettered).
            Assert.Equal(2, flakyServices.ScopeCreations);
            Assert.Equal(1, logger.Entries.Count(e => e.Level == LogLevel.Warning));
            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
            Assert.Empty(await DrainDeadLetterQueue(pubsub));

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var persisted = await verifyContext.Players.FindAsync([player.Id], CancellationToken);
            Assert.NotNull(persisted);
            Assert.Equal(12, persisted.Level);
            Assert.Equal(4321, persisted.Exp);
        }

        [Fact]
        public async Task ProcessQueue_WellFormedEnvelopeWithMalformedInnerPayload_DeadLettersWithoutRetrying()
        {
            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var logger = new CapturingLogger<DataProviderSynchronizer>();
            var synchronizer = new DataProviderSynchronizer(scope.ServiceProvider, pubsub, logger, TestRetryPolicy);

            // The envelope parses cleanly (known event type), but its inner payload is not valid JSON, so
            // HandleEvent throws a JsonException — a poison message that must be dead-lettered without retrying.
            var envelope = new DomainEventEnvelope
            {
                Type = nameof(PlayerCoreUpdatedEvent),
                Payload = "this is not valid json",
            };
            var message = envelope.Serialize();
            var queue = new InMemoryPubSubQueue(message);

            await synchronizer.ProcessQueue(queue);

            // A poison inner payload is surfaced as a warning (not retried, so no error) and dead-lettered verbatim.
            Assert.Equal(1, logger.Entries.Count(e => e.Level == LogLevel.Warning));
            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
            Assert.Equal([message], await DrainDeadLetterQueue(pubsub));
        }

        [Fact]
        public async Task ProcessQueue_UnknownEventType_DeadLettersWithoutRetrying()
        {
            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var logger = new CapturingLogger<DataProviderSynchronizer>();
            var synchronizer = new DataProviderSynchronizer(scope.ServiceProvider, pubsub, logger, TestRetryPolicy);

            // A well-formed envelope whose Type matches no registered handler case — e.g. a new event
            // added to PlayerPersistencePublisher without a corresponding case in HandleEvent.
            var envelope = new DomainEventEnvelope
            {
                Type = "UnregisteredEventType",
                Payload = "{}",
            };
            var message = envelope.Serialize();
            var queue = new InMemoryPubSubQueue(message);

            await synchronizer.ProcessQueue(queue);

            // An unknown type is a poison message — no retry can fix it — so it is dead-lettered
            // immediately with a warning and without escalating to an error.
            Assert.Equal(1, logger.Entries.Count(e => e.Level == LogLevel.Warning));
            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
            Assert.Equal([message], await DrainDeadLetterQueue(pubsub));
        }

        [Fact]
        public async Task ProcessQueue_SkillUnlockedEvent_InsertsUnselectedPlayerSkillIdempotently()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, level: 5, zoneId: 0);
            var skill = await TestDataSeeder.CreateSkillAsync(context);

            var evt = new SkillUnlockedEvent(player.Id, skill.Id);

            var logger = new CapturingLogger<DataProviderSynchronizer>();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var synchronizer = new DataProviderSynchronizer(scope.ServiceProvider, pubsub, logger, TestRetryPolicy);

            // The same unlock is delivered twice (e.g. a retry, or two challenges granting the same skill);
            // the idempotent insert must leave exactly one row, unselected and at order 0.
            var queue = new InMemoryPubSubQueue(Serialize(evt), Serialize(evt));

            await synchronizer.ProcessQueue(queue);

            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
            Assert.Empty(await DrainDeadLetterQueue(pubsub));

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var rows = await verifyContext.PlayerSkills
                .Where(ps => ps.PlayerId == player.Id && ps.SkillId == skill.Id)
                .ToListAsync(CancellationToken);
            var row = Assert.Single(rows);
            Assert.False(row.Selected);
            Assert.Equal(0, row.Order);
        }

        [Fact]
        public async Task ProcessQueue_SelectedSkillsChangedEvent_ReplacesEquippedSetAndOrderIdempotently()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, level: 5, zoneId: 0);
            var skill0 = await TestDataSeeder.CreateSkillAsync(context, name: "S0");
            var skill1 = await TestDataSeeder.CreateSkillAsync(context, name: "S1");
            var skill2 = await TestDataSeeder.CreateSkillAsync(context, name: "S2");

            // Starting loadout: skill0 (order 0), skill1 (order 1) equipped; skill2 unlocked but not equipped.
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill0.Id, selected: true, order: 0);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill1.Id, selected: true, order: 1);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill2.Id, selected: false, order: 0);

            // New loadout swaps skill1 out for skill2 and reverses the survivor's position.
            var evt = new SelectedSkillsChangedEvent(player.Id, [skill2.Id, skill0.Id]);

            var logger = new CapturingLogger<DataProviderSynchronizer>();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var synchronizer = new DataProviderSynchronizer(scope.ServiceProvider, pubsub, logger, TestRetryPolicy);

            // Deliver the same event twice: the delete-then-rebuild handler must converge to one result.
            var queue = new InMemoryPubSubQueue(Serialize(evt), Serialize(evt));

            await synchronizer.ProcessQueue(queue);

            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
            Assert.Empty(await DrainDeadLetterQueue(pubsub));

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var rows = await verifyContext.PlayerSkills
                .Where(ps => ps.PlayerId == player.Id)
                .ToListAsync(CancellationToken);

            Assert.Equal(3, rows.Count);
            // skill2 is now first, skill0 second; skill1 is deselected and its order reset to 0.
            AssertSkillState(rows, skill2.Id, selected: true, order: 0);
            AssertSkillState(rows, skill0.Id, selected: true, order: 1);
            AssertSkillState(rows, skill1.Id, selected: false, order: 0);
        }

        [Fact]
        public async Task ProcessQueue_SelectedSkillsChangedEvent_ReorderOnly_UpdatesOrder()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, level: 5, zoneId: 0);
            var skill0 = await TestDataSeeder.CreateSkillAsync(context, name: "S0");
            var skill1 = await TestDataSeeder.CreateSkillAsync(context, name: "S1");

            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill0.Id, selected: true, order: 0);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill1.Id, selected: true, order: 1);

            // Same set, swapped positions.
            var evt = new SelectedSkillsChangedEvent(player.Id, [skill1.Id, skill0.Id]);

            var logger = new CapturingLogger<DataProviderSynchronizer>();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var synchronizer = new DataProviderSynchronizer(scope.ServiceProvider, pubsub, logger, TestRetryPolicy);

            await synchronizer.ProcessQueue(new InMemoryPubSubQueue(Serialize(evt)));

            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var rows = await verifyContext.PlayerSkills
                .Where(ps => ps.PlayerId == player.Id)
                .ToListAsync(CancellationToken);

            AssertSkillState(rows, skill1.Id, selected: true, order: 0);
            AssertSkillState(rows, skill0.Id, selected: true, order: 1);
        }

        [Fact]
        public async Task ProcessQueue_SelectedSkillsChangedEvent_DeselectToEmpty_ClearsAllSelections()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, level: 5, zoneId: 0);
            var skill0 = await TestDataSeeder.CreateSkillAsync(context, name: "S0");
            var skill1 = await TestDataSeeder.CreateSkillAsync(context, name: "S1");

            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill0.Id, selected: true, order: 0);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill1.Id, selected: true, order: 1);

            // An empty loadout deselects everything (the skills stay unlocked).
            var evt = new SelectedSkillsChangedEvent(player.Id, []);

            var logger = new CapturingLogger<DataProviderSynchronizer>();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var synchronizer = new DataProviderSynchronizer(scope.ServiceProvider, pubsub, logger, TestRetryPolicy);

            await synchronizer.ProcessQueue(new InMemoryPubSubQueue(Serialize(evt)));

            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var rows = await verifyContext.PlayerSkills
                .Where(ps => ps.PlayerId == player.Id)
                .ToListAsync(CancellationToken);

            // Both skills remain unlocked but are no longer equipped, with order reset to 0.
            Assert.Equal(2, rows.Count);
            AssertSkillState(rows, skill0.Id, selected: false, order: 0);
            AssertSkillState(rows, skill1.Id, selected: false, order: 0);
        }

        private static void AssertSkillState(IEnumerable<Infrastructure.Entities.PlayerSkill> rows, int skillId, bool selected, int order)
        {
            var row = Assert.Single(rows, ps => ps.SkillId == skillId);
            Assert.Equal(selected, row.Selected);
            Assert.Equal(order, row.Order);
        }

        private static async Task<List<string>> DrainDeadLetterQueue(IPubSubService pubsub)
        {
            var deadLetterQueue = pubsub.GetQueue(Constants.PUBSUB_PLAYER_DEAD_LETTER_QUEUE);
            var drained = new List<string>();
            var next = await deadLetterQueue.GetNextAsync();
            while (next is not null)
            {
                drained.Add(next);
                next = await deadLetterQueue.GetNextAsync();
            }

            return drained;
        }

        private static string Serialize<T>(T evt) where T : IDomainEvent
        {
            var envelope = new DomainEventEnvelope
            {
                Type = typeof(T).Name,
                Payload = evt.Serialize(),
            };

            return envelope.Serialize();
        }

        /// <summary>
        /// Minimal in-memory <see cref="IPubSubQueue"/> so the queue-processing loop can be driven deterministically
        /// without depending on the Redis pub/sub background worker (which the integration harness intentionally disables).
        /// </summary>
        private sealed class InMemoryPubSubQueue : IPubSubQueue
        {
            private readonly Queue<string?> _items;

            public InMemoryPubSubQueue(params string?[] items)
            {
                _items = new Queue<string?>(items);
            }

            public string? GetNext() => _items.Count > 0 ? _items.Dequeue() : null;
            public Task<string?> GetNextAsync() => Task.FromResult(GetNext());
            public void AddToQueue(string value) => _items.Enqueue(value);
            public Task AddToQueueAsync(string value)
            {
                _items.Enqueue(value);
                return Task.CompletedTask;
            }

            // Not exercised by DataProviderSynchronizer.ProcessQueue.
            public T? GetNext<T>() => throw new NotSupportedException();
            public Task<T?> GetNextAsync<T>() => throw new NotSupportedException();
            public void AddToQueue<T>(T value) => throw new NotSupportedException();
            public Task AddToQueueAsync<T>(T value) => throw new NotSupportedException();
        }

        /// <summary>
        /// Wraps a real <see cref="IServiceProvider"/> but makes the first <paramref name="failuresBeforeSuccess"/>
        /// scope creations throw, simulating a transient failure that succeeds on a later retry. Scopes are created by
        /// <see cref="ServiceProviderServiceExtensions.CreateScope"/>, which resolves this provider as the
        /// <see cref="IServiceScopeFactory"/>, so failing here mirrors a transient error setting up the unit of work.
        /// </summary>
        private sealed class FlakyServiceProvider(IServiceProvider inner, int failuresBeforeSuccess) : IServiceProvider, IServiceScopeFactory
        {
            public int ScopeCreations { get; private set; }

            public object? GetService(Type serviceType)
            {
                if (serviceType == typeof(IServiceScopeFactory))
                {
                    return this;
                }

                return inner.GetService(serviceType);
            }

            public IServiceScope CreateScope()
            {
                ScopeCreations++;
                if (ScopeCreations <= failuresBeforeSuccess)
                {
                    throw new InvalidOperationException("Simulated transient failure creating a scope.");
                }

                return inner.GetRequiredService<IServiceScopeFactory>().CreateScope();
            }
        }

        private sealed class CapturingLogger<T> : ILogger<T>
        {
            public List<LogEntry> Entries { get; } = [];

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
            }

            public record LogEntry(LogLevel Level, string Message, Exception? Exception);
        }
    }
}
