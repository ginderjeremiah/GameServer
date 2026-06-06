using Game.Abstractions.Infrastructure;
using Game.Core.Players.Events;
using Game.DataAccess;
using Game.DataAccess.Repositories;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Verifies that <see cref="DataProviderSynchronizer"/> no longer silently swallows exceptions while draining the
    /// player update queue: malformed payloads are logged as warnings and unexpected failures as errors, and in both
    /// cases the failing message does not stop the remaining queued events from being processed.
    /// </summary>
    [Collection("Integration")]
    public class DataProviderSynchronizerTests : ApplicationIntegrationTestBase
    {
        public DataProviderSynchronizerTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        [Fact]
        public async Task ProcessQueue_MalformedEventBeforeValidEvent_LogsWarningAndStillPersistsValidEvent()
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
            var synchronizer = new DataProviderSynchronizer(scope.ServiceProvider, pubsub, logger);

            // The malformed message is dequeued first; the synchronizer must skip it and still apply the valid one.
            var queue = new InMemoryPubSubQueue("this is not a valid envelope", Serialize(validEvent));

            await synchronizer.ProcessQueue(queue);

            // The malformed payload is surfaced as a warning rather than silently swallowed.
            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);

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
        public async Task ProcessQueue_UnexpectedFailureDuringHandling_LogsErrorAndContinues()
        {
            // An empty provider has no GameContext registered, so HandleEvent throws InvalidOperationException —
            // standing in for any unexpected failure (e.g. a database error) while persisting a player change.
            var brokenServices = new ServiceCollection().BuildServiceProvider();

            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var logger = new CapturingLogger<DataProviderSynchronizer>();
            var synchronizer = new DataProviderSynchronizer(brokenServices, pubsub, logger);

            var firstEvent = new PlayerCoreUpdatedEvent(1, 2, 3, 0, 100, 100);
            var secondEvent = new PlayerCoreUpdatedEvent(2, 3, 4, 0, 100, 100);
            var queue = new InMemoryPubSubQueue(Serialize(firstEvent), Serialize(secondEvent));

            await synchronizer.ProcessQueue(queue);

            // Both unexpected failures are logged as errors (not warnings), and the first failure did not stop the second.
            Assert.Equal(2, logger.Entries.Count(e => e.Level == LogLevel.Error));
            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
            Assert.Null(await queue.GetNextAsync());
        }

        private static string Serialize(PlayerCoreUpdatedEvent evt)
        {
            var envelope = new DomainEventEnvelope
            {
                Type = nameof(PlayerCoreUpdatedEvent),
                Payload = JsonSerializer.Serialize(evt),
            };
            return JsonSerializer.Serialize(envelope);
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
