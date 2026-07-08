using Game.Abstractions.DataAccess;
using Game.Abstractions.Infrastructure;
using Game.Api;
using Game.Api.Services;
using Game.Api.Sockets;
using Game.Api.Sockets.Commands;
using Game.Application;
using Game.Core.Players;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Game.Api.Tests.Unit
{
    /// <summary>
    /// Verifies the server-initiated (pub/sub) command processor's drain-and-escalate contract (#656, #671):
    /// <see cref="SocketManagerService.GetSocketCommandProcessor"/> isolates a faulting command and keeps
    /// draining the queue so a later command still runs, and a genuine fault is no longer silently dropped —
    /// the poisoned payload is dead-lettered and the client is sent a <see cref="ServerCommandFailed"/> notice.
    /// A <see cref="FakeWebSocket"/> and an in-memory queue stand in for the transport and the Redis backplane,
    /// so these run as plain unit tests with hand-built dependencies — see <c>docs/backend.md</c> →
    /// "Challenge-completion notifications (server push)".
    /// </summary>
    public sealed class SocketCommandProcessorTests : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly ILoggerFactory _loggerFactory;
        private readonly CapturingLoggerProvider _logs = new();

        public SocketCommandProcessorTests()
        {
            _provider = new ServiceCollection()
                .AddScoped<IUnitOfWork, NoOpUnitOfWork>()
                .BuildServiceProvider();
            _loggerFactory = LoggerFactory.Create(b => b.AddProvider(_logs).SetMinimumLevel(LogLevel.Trace));
        }

        [Fact]
        public async Task Processor_CommandFaultThenValidCommand_QueueKeepsDrainingAndEscalatesTheFault()
        {
            var commands = new CapturingCommandFactory(throwOn: name => name == "WillFault" ? new InvalidOperationException("boom") : null);
            var (processor, pubSub) = BuildProcessor(commands);

            var queue = new FakeQueue(
                new SocketCommandInfo("WillFault") { Id = "fault-1" },
                new SocketCommandInfo("WillSucceed") { Id = "ok-1" });

            // The processor must not propagate the fault out of its drain loop.
            await processor(queue);

            // Both real commands ran in order — the fault did not abort the drain — and the failed push was
            // surfaced to the client with a ServerCommandFailed notice dispatched between them.
            Assert.Equal(["WillFault", "ServerCommandFailed", "WillSucceed"], commands.ExecutedCommandNames);
            // The faulted payload is dead-lettered (not silently dropped) and logged, while the following
            // command still produces its own (successful) response.
            Assert.Contains(pubSub.DeadLetterQueue.Added, m => m.Contains("fault-1"));
            Assert.Contains(_logs.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("fault-1"));
            Assert.DoesNotContain(_logs.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("ok-1"));
        }

        [Fact]
        public async Task Processor_ServerCommandFaults_DeadLettersPayloadAndNotifiesTheClient()
        {
            var commands = new CapturingCommandFactory(throwOn: name => name == "Poison" ? new InvalidOperationException("boom") : null);
            var (processor, pubSub) = BuildProcessor(commands);

            var queue = new FakeQueue(new SocketCommandInfo("Poison") { Id = "poison-1", Parameters = "bad" });

            await processor(queue);

            // The faulted payload is preserved on the dead-letter queue rather than silently dropped, carrying
            // enough to inspect/replay it (its name and id).
            var deadLettered = Assert.Single(pubSub.DeadLetterQueue.Added);
            Assert.Contains("Poison", deadLettered);
            Assert.Contains("poison-1", deadLettered);

            // The client is told a server push failed via a ServerCommandFailed notice (surface-to-client),
            // and the escalation is recorded at warning.
            Assert.Contains("ServerCommandFailed", commands.ExecutedCommandNames);
            Assert.Contains(_logs.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("Dead-lettered"));
        }

        [Fact]
        public async Task Processor_ResyncNoticeAlsoFails_WarnsThatTheClientMayHaveMissedTheCue()
        {
            // The original push faults and is escalated; if the ServerCommandFailed re-sync notice itself does
            // not get through (here it also faults), the client never receives the re-sync cue for a gating
            // command and would diverge silently — so the lost cue is surfaced at warning (#953), not ignored.
            var commands = new CapturingCommandFactory(throwOn: name =>
                name is "ChallengeCompleted" or "ServerCommandFailed" ? new InvalidOperationException("boom") : null);
            var (processor, _) = BuildProcessor(commands);

            var queue = new FakeQueue(new SocketCommandInfo("ChallengeCompleted") { Id = "push-1" });

            await processor(queue);

            Assert.Contains(_logs.Entries, e => e.Level == LogLevel.Warning
                && e.Message.Contains("may not have received the re-sync cue")
                && e.Message.Contains("ChallengeCompleted"));
        }

        [Fact]
        public async Task Processor_DequeueFaultThenValidCommand_QueueKeepsDrainingAndProcessorDoesNotThrow()
        {
            var commands = new CapturingCommandFactory(throwOn: _ => null);
            var (processor, _) = BuildProcessor(commands);

            // The first dequeue faults (a malformed payload / Redis blip), which previously escaped the loop
            // outside the try and killed the drain — silently dropping every later push (#691). It must be
            // logged and skipped so the following command still runs.
            var queue = new FakeQueue(
                FakeQueueStep.Throw(new InvalidOperationException("dequeue boom")),
                FakeQueueStep.Yield(new SocketCommandInfo("WillSucceed") { Id = "ok-1" }));

            await processor(queue);

            // The valid command after the dequeue fault still ran, proving the loop survived.
            Assert.Equal(["ok-1"], commands.ExecutedCommandIds);
            Assert.Contains(_logs.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("dequeuing"));
        }

        [Fact]
        public async Task Processor_EmptyQueue_CompletesWithoutDispatchingOrThrowing()
        {
            var commands = new CapturingCommandFactory(throwOn: _ => null);
            var (processor, _) = BuildProcessor(commands);

            await processor(new FakeQueue());

            Assert.Empty(commands.ExecutedCommandIds);
        }

        [Fact]
        public async Task RegisterSocket_SubscribeFailsAfterPresenceWrite_RollsBackPresenceClaim()
        {
            // The presence key write succeeds (GetSet returns null — no prior owner), then Subscribe throws.
            // Without rollback the key would point at a socket whose drain loops never started — a "registered
            // but dead" presence that blocks the player forever (#691). Rollback must compare-and-delete it.
            var cache = new RecordingCacheService();
            var pubSub = new ThrowingSubscribePubSubService(new InvalidOperationException("subscribe boom"));
            var scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();
            var registry = new SocketConnectionRegistry(new NoOpHostLifetime(), NullLogger<SocketConnectionRegistry>.Instance);
            var manager = new SocketManagerService(
                pubSub, cache, new CapturingCommandFactory(_ => null), scopeFactory, _loggerFactory, registry);

            var socket = new FakeWebSocket(sendDuration: TimeSpan.Zero);
            var session = new SessionService(new NoOpSessionStore());
            session.CreateSession(userId: 1, playerId: 42);

            // The Subscribe failure propagates out of RegisterSocket...
            await Assert.ThrowsAsync<InvalidOperationException>(() => manager.RegisterSocket(socket, session, isAdmin: false));

            // ...and the partial registration was rolled back: the presence key was released via a
            // compare-and-delete keyed on the exact socket id that was claimed (so a newer owner's key would be
            // left intact), on the player's presence key.
            var claim = Assert.Single(cache.GetSetWithExpiries);
            var release = Assert.Single(cache.CompareAndDeletes);
            Assert.Equal(claim.Key, release.Key);
            Assert.Equal(claim.Value, release.DeleteIfValue);
            Assert.Contains("42", release.Key);
        }

        [Fact]
        public async Task Processor_PersistentDequeueFault_AbandonsAfterCeilingInsteadOfHotSpinning()
        {
            var commands = new CapturingCommandFactory(throwOn: _ => null);
            var (processor, _) = BuildProcessor(commands);

            // Every dequeue throws (Redis down, or a throw before the pop). Without a ceiling the loop would
            // hot-spin for the life of the connection, hammering Redis and the log (#909); it must back off
            // and give up after a bounded number of consecutive failures, returning rather than looping.
            var queue = new AlwaysThrowingQueue(new InvalidOperationException("dequeue boom"));

            await processor(queue);

            // The processor stopped after exactly the consecutive-failure ceiling rather than spinning, and
            // it logged the abandonment so the give-up is observable.
            Assert.Equal(SocketManagerService.MaxConsecutiveDequeueFailures, queue.Attempts);
            Assert.Contains(_logs.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("Abandoning"));
        }

        [Fact]
        public async Task UnRegisterSocket_UnsubscribeThrows_StillReleasesPresenceKey()
        {
            // The clean-disconnect teardown runs the same guarded best-effort steps as a rollback: even when
            // the unsubscribe throws, the presence-key release must still run so the key can't survive its
            // full TTL reporting a ghost session that makes HasActiveSocket lie until expiry (#909).
            var cache = new RecordingCacheService();
            var pubSub = new ThrowingUnsubscribePubSubService(new InvalidOperationException("unsubscribe boom"));
            var scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();
            var registry = new SocketConnectionRegistry(new NoOpHostLifetime(), NullLogger<SocketConnectionRegistry>.Instance);
            var manager = new SocketManagerService(
                pubSub, cache, new CapturingCommandFactory(_ => null), scopeFactory, _loggerFactory, registry);

            var socket = new FakeWebSocket(sendDuration: TimeSpan.Zero);
            var session = new SessionService(new NoOpSessionStore());
            session.CreateSession(userId: 1, playerId: 77);
            var context = await manager.RegisterSocket(socket, session, isAdmin: false);

            // The unsubscribe fault during teardown must be swallowed, not propagated...
            await manager.UnRegisterSocket(context);

            // ...and the presence key was still released via a compare-and-delete keyed on the socket's own
            // id (so a newer owner's key would be left intact), on the player's presence key.
            var release = Assert.Single(cache.CompareAndDeletes);
            Assert.Contains("77", release.Key);
            Assert.Equal(context.SocketId, release.DeleteIfValue);
            Assert.Contains(_logs.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("unsubscribe"));

            // ...and the socket's own command queue was deleted too, so a graceful disconnect never strands a
            // permanent queue key waiting out its backstop TTL (#1498).
            Assert.Contains($"{Constants.PUBSUB_SOCKET_QUEUE_PREFIX}_{context.SocketId}", cache.Deletes);
        }

        [Fact]
        public async Task EmitSocketCommand_BySocketId_RefreshesTheQueueKeyTtlAfterPublishing()
        {
            // Every push refreshes the per-socket queue key's TTL — a backstop against a permanent, TTL-less
            // Redis key for a push that races a disconnect or is never drained (#1498). The refresh happens
            // after the durable publish, not instead of it.
            var cache = new RecordingCacheService();
            var pubSub = new CapturingPubSubService();
            var scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();
            var registry = new SocketConnectionRegistry(new NoOpHostLifetime(), NullLogger<SocketConnectionRegistry>.Instance);
            var manager = new SocketManagerService(
                pubSub, cache, new CapturingCommandFactory(_ => null), scopeFactory, _loggerFactory, registry);

            await manager.EmitSocketCommand(new SocketCommandInfo("ChallengeCompleted"), socketId: "socket-42");

            Assert.Equal($"{Constants.PUBSUB_SOCKET_QUEUE_PREFIX}_socket-42", Assert.Single(cache.Expires).Key);
        }

        private (Func<IPubSubQueue, Task> Processor, CapturingPubSubService PubSub) BuildProcessor(CapturingCommandFactory commandFactory)
        {
            var capturingPubSub = new CapturingPubSubService();
            var scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();
            var registry = new SocketConnectionRegistry(new NoOpHostLifetime(), NullLogger<SocketConnectionRegistry>.Instance);

            var manager = new SocketManagerService(
                capturingPubSub, new NoOpCacheService(), commandFactory, scopeFactory, _loggerFactory, registry);

            var socket = new FakeWebSocket(sendDuration: TimeSpan.Zero);
            var session = new SessionService(new NoOpSessionStore());
            session.CreateSession(userId: 1, playerId: 1);
            // RegisterSocket wires the real processor into the fake pub/sub via RegisterSocketCommandListener,
            // so the captured callback is the production GetSocketCommandProcessor closure under test.
            manager.RegisterSocket(socket, session, isAdmin: false).GetAwaiter().GetResult();

            var processor = capturingPubSub.CapturedProcessor
                ?? throw new InvalidOperationException("Processor was not registered.");
            return (processor, capturingPubSub);
        }

        public void Dispose()
        {
            _loggerFactory.Dispose();
            _provider.Dispose();
        }

        /// <summary>A command factory that records the ids it executes and, per command name, optionally
        /// produces a command that throws — so a test can drive a genuine fault followed by a clean command
        /// and assert the queue kept draining.</summary>
        private sealed class CapturingCommandFactory(Func<string, Exception?> throwOn) : SocketCommandFactory
        {
            public List<string> ExecutedCommandIds { get; } = [];
            public List<string> ExecutedCommandNames { get; } = [];

            public override AbstractSocketCommand CreateCommand(SocketCommandInfo commandInfo, IServiceScope scope)
            {
                ExecutedCommandIds.Add(commandInfo.Id ?? "<null>");
                ExecutedCommandNames.Add(commandInfo.Name);
                return new StubCommand(commandInfo, throwOn(commandInfo.Name));
            }
        }

        private sealed class StubCommand(SocketCommandInfo info, Exception? toThrow) : AbstractSocketCommand
        {
            public override string Name { get; set; } = info.Name;

            public override Task<Game.Api.Models.Common.ApiSocketResponse> ExecuteAsync(SocketContext context, CancellationToken cancellationToken)
            {
                if (toThrow is not null)
                {
                    throw toThrow;
                }

                return Task.FromResult(Success());
            }
        }

        /// <summary>A scripted step for <see cref="FakeQueue"/>: either yield a command or throw on dequeue.
        /// An implicit conversion from <see cref="SocketCommandInfo"/> keeps the plain command cases terse.</summary>
        private sealed class FakeQueueStep
        {
            public SocketCommandInfo? Command { get; private init; }
            public Exception? Error { get; private init; }

            public static FakeQueueStep Yield(SocketCommandInfo command) => new() { Command = command };
            public static FakeQueueStep Throw(Exception error) => new() { Error = error };

            public static implicit operator FakeQueueStep(SocketCommandInfo command) => Yield(command);
        }

        private sealed class FakeQueue(params FakeQueueStep[] steps) : IPubSubQueue
        {
            private readonly Queue<FakeQueueStep> _steps = new(steps);

            public Task<T?> GetNextAsync<T>(CancellationToken cancellationToken = default)
            {
                if (_steps.TryDequeue(out var next))
                {
                    if (next.Error is not null)
                    {
                        throw next.Error;
                    }

                    if (next.Command is T typed)
                    {
                        return Task.FromResult<T?>(typed);
                    }
                }

                return Task.FromResult<T?>(default);
            }

            public Task<string?> GetNextAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<string?> ReserveNextAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task AcknowledgeAsync(string value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<long> ReclaimProcessingAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<long> GetLengthAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<long> GetProcessingCountAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<IReadOnlyList<string>> PeekAsync(long count, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<bool> RemoveAsync(string value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task AddToQueueAsync(string value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task AddToQueueAsync<T>(T value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task AddRangeToQueueAsync(IEnumerable<string> values, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        }

        /// <summary>An in-memory queue whose every dequeue throws, to drive the processor's consecutive-
        /// failure ceiling. Counts the attempts so a test can assert it gave up rather than hot-spinning.</summary>
        private sealed class AlwaysThrowingQueue(Exception toThrow) : IPubSubQueue
        {
            public int Attempts { get; private set; }

            public Task<T?> GetNextAsync<T>(CancellationToken cancellationToken = default)
            {
                Attempts++;
                throw toThrow;
            }

            public Task<string?> GetNextAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<string?> ReserveNextAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task AcknowledgeAsync(string value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<long> ReclaimProcessingAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<long> GetLengthAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<long> GetProcessingCountAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<IReadOnlyList<string>> PeekAsync(long count, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<bool> RemoveAsync(string value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task AddToQueueAsync(string value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task AddToQueueAsync<T>(T value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task AddRangeToQueueAsync(IEnumerable<string> values, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        }

        private sealed class CapturingPubSubService : IPubSubService
        {
            public Func<IPubSubQueue, Task>? CapturedProcessor { get; private set; }

            // The dead-letter queue the escalation writes a faulted server command to (the only GetQueue use).
            public RecordingQueue DeadLetterQueue { get; } = new();

            public Task Subscribe(string channel, string queueName, Func<(IPubSubQueue queue, string channel), Task> action, string id)
            {
                CapturedProcessor = queue => action((queue, channel));
                return Task.CompletedTask;
            }

            public Task Subscribe(string channel, Action<(string message, string channel)> action, string? id = null) => Task.CompletedTask;
            public Task Publish(string channel, string message, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task Publish(string channel, string queueName, string queueData, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task Publish<T>(string channel, string queueName, T queueData, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task PublishBatch<T>(string channel, string queueName, IEnumerable<T> queueData, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task Wake(string channel) => Task.CompletedTask;
            public Task UnSubscribe(string channel, string id) => Task.CompletedTask;
            public IPubSubQueue GetQueue(string queueName) => DeadLetterQueue;
        }

        /// <summary>An in-memory queue that records the values written to it, so a test can assert a faulted
        /// server command's payload was dead-lettered rather than dropped.</summary>
        private sealed class RecordingQueue : IPubSubQueue
        {
            public List<string> Added { get; } = [];

            public Task AddToQueueAsync(string value, CancellationToken cancellationToken = default)
            {
                Added.Add(value);
                return Task.CompletedTask;
            }

            public Task<string?> GetNextAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<T?> GetNextAsync<T>(CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<string?> ReserveNextAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task AcknowledgeAsync(string value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<long> ReclaimProcessingAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
            // The escalation path reads the depth right after adding, to log it alongside the escalation.
            public Task<long> GetLengthAsync(CancellationToken cancellationToken = default) => Task.FromResult((long)Added.Count);
            public Task<long> GetProcessingCountAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<IReadOnlyList<string>> PeekAsync(long count, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<bool> RemoveAsync(string value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task AddToQueueAsync<T>(T value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task AddRangeToQueueAsync(IEnumerable<string> values, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        }

        // RegisterSocket only claims the presence key via the atomic GetSet-with-expiry; everything else is unused here.
        private sealed class NoOpCacheService : ICacheService
        {
            public Task<string?> GetSet(string key, string value, TimeSpan expiry, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

            public Task<string?> Get(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<T?> Get<T>(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<string?> GetDelete(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<T?> GetDelete<T>(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task Set(string key, string? value, TimeSpan expiry, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task Set<T>(string key, T value, TimeSpan expiry, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public void ExpireAndForget(string key, TimeSpan expiry) => throw new NotSupportedException();
            public void SetAndForget(string key, string? value, TimeSpan expiry) => throw new NotSupportedException();
            public void SetAndForget<T>(string key, T value, TimeSpan expiry) => throw new NotSupportedException();
            public Task Delete(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public void DeleteAndForget(string key) => throw new NotSupportedException();
            public Task CompareAndDelete(string key, string deleteIfValue, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<bool> CompareAndSet(string key, string? expectedValue, string newValue, TimeSpan expiry, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public void ReclaimAndForget(string key, string ownerValue, TimeSpan expiry) => throw new NotSupportedException();
            public Task<Dictionary<string, string>?> HashGetAllIfExists(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public void HashSetAndForget(string key, IReadOnlyDictionary<string, string> fields, TimeSpan expiry) => throw new NotSupportedException();
        }

        /// <summary>A pub/sub whose <c>Subscribe</c> throws, to drive the registration-rollback path
        /// (whose best-effort unsubscribe steps are tolerated as no-ops).</summary>
        private sealed class ThrowingSubscribePubSubService(Exception toThrow) : NotSupportedPubSubService
        {
            public override Task Subscribe(string channel, string queueName, Func<(IPubSubQueue queue, string channel), Task> action, string id) => throw toThrow;

            public override Task UnSubscribe(string channel, string id) => Task.CompletedTask;
        }

        /// <summary>A pub/sub whose <c>Subscribe</c> succeeds but <c>UnSubscribe</c> throws, to drive the
        /// guarded-teardown path where the presence-key release must still run after an unsubscribe fault.</summary>
        private sealed class ThrowingUnsubscribePubSubService(Exception toThrow) : NotSupportedPubSubService
        {
            public override Task Subscribe(string channel, string queueName, Func<(IPubSubQueue queue, string channel), Task> action, string id) => Task.CompletedTask;
            public override Task UnSubscribe(string channel, string id) => throw toThrow;
        }

        /// <summary>Records the presence-key writes and releases so a rollback test can assert the exact
        /// claimed socket id is the one compare-and-deleted.</summary>
        private sealed class RecordingCacheService : ICacheService
        {
            public List<(string Key, string? Value)> GetSetWithExpiries { get; } = [];
            public List<(string Key, string DeleteIfValue)> CompareAndDeletes { get; } = [];
            public List<string> Deletes { get; } = [];
            public List<(string Key, TimeSpan Expiry)> Expires { get; } = [];

            public Task<string?> GetSet(string key, string value, TimeSpan expiry, CancellationToken cancellationToken = default)
            {
                GetSetWithExpiries.Add((key, value));
                return Task.FromResult<string?>(null);
            }

            public Task CompareAndDelete(string key, string deleteIfValue, CancellationToken cancellationToken = default)
            {
                CompareAndDeletes.Add((key, deleteIfValue));
                return Task.CompletedTask;
            }

            // The socket command queue delete during teardown is unconditional (unlike the compare-and-delete
            // presence key), so it just needs to succeed rather than be asserted on by every test here.
            public Task Delete(string key, CancellationToken cancellationToken = default)
            {
                Deletes.Add(key);
                return Task.CompletedTask;
            }

            public void ExpireAndForget(string key, TimeSpan expiry)
            {
                Expires.Add((key, expiry));
            }

            public Task<string?> Get(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<T?> Get<T>(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<string?> GetDelete(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<T?> GetDelete<T>(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task Set(string key, string? value, TimeSpan expiry, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task Set<T>(string key, T value, TimeSpan expiry, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public void SetAndForget(string key, string? value, TimeSpan expiry) => throw new NotSupportedException();
            public void SetAndForget<T>(string key, T value, TimeSpan expiry) => throw new NotSupportedException();
            public void DeleteAndForget(string key) => throw new NotSupportedException();
            public Task<bool> CompareAndSet(string key, string? expectedValue, string newValue, TimeSpan expiry, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public void ReclaimAndForget(string key, string ownerValue, TimeSpan expiry) => throw new NotSupportedException();
            public Task<Dictionary<string, string>?> HashGetAllIfExists(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public void HashSetAndForget(string key, IReadOnlyDictionary<string, string> fields, TimeSpan expiry) => throw new NotSupportedException();
        }

        private sealed class NoOpHostLifetime : IHostApplicationLifetime
        {
            public CancellationToken ApplicationStarted => CancellationToken.None;
            public CancellationToken ApplicationStopping => CancellationToken.None;
            public CancellationToken ApplicationStopped => CancellationToken.None;
            public void StopApplication() { }
        }

        private sealed class NoOpSessionStore : ISessionStore
        {
            public Task<PlayerState?> GetSession(int userId, CancellationToken cancellationToken = default) => Task.FromResult<PlayerState?>(null);
            public void Update(PlayerState sessionData, int playerId) { }
            public void Clear(int userId) { }
        }

        private sealed class NoOpUnitOfWork : IUnitOfWork
        {
            public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
    }
}
