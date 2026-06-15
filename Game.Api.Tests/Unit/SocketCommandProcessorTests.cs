using Game.Abstractions.DataAccess;
using Game.Abstractions.Infrastructure;
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
    /// Verifies the server-initiated (pub/sub) command processor's drain contract (#656):
    /// <see cref="SocketManagerService.GetSocketCommandProcessor"/> isolates a faulting command and keeps
    /// draining the queue so a later command still runs, and never lets an exception escape its loop.
    /// A <see cref="FakeWebSocket"/> and an in-memory queue stand in for the transport and the Redis
    /// backplane, so these run as plain unit tests with hand-built dependencies — see <c>docs/backend.md</c>
    /// → "Challenge-completion notifications (server push)".
    ///
    /// Note the actual failure handler is <see cref="SocketHandler.ExecuteCommand"/>, whose own catch absorbs
    /// every command-execution fault (turning it into an "Internal Server Error" response — which goes nowhere
    /// for a server push). The processor's own cancellation/fault catch is a backstop for the narrow residual
    /// that escapes ExecuteCommand; since no lifetime token is plumbed into command execution today, asserting
    /// that backstop's log levels through the full path would be testing currently-unreachable code, so these
    /// tests assert the reachable contract — the queue keeps draining and the processor never throws.
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
        public async Task Processor_CommandFaultThenValidCommand_QueueKeepsDrainingAndProcessorDoesNotThrow()
        {
            var commands = new CapturingCommandFactory(throwOn: name => name == "WillFault" ? new InvalidOperationException("boom") : null);
            var processor = BuildProcessor(commands);

            var queue = new FakeQueue(
                new SocketCommandInfo("WillFault") { Id = "fault-1" },
                new SocketCommandInfo("WillSucceed") { Id = "ok-1" });

            // The processor must not propagate the fault out of its drain loop.
            await processor(queue);

            // Both commands ran in order — the first command's fault did not abort the drain.
            Assert.Equal(["fault-1", "ok-1"], commands.ExecutedCommandIds);
            // The fault is logged (by ExecuteCommand's own handler) rather than silently dropped, while the
            // following command still produces its own (successful) response.
            Assert.Contains(_logs.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("fault-1"));
            Assert.DoesNotContain(_logs.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("ok-1"));
        }

        [Fact]
        public async Task Processor_DequeueFaultThenValidCommand_QueueKeepsDrainingAndProcessorDoesNotThrow()
        {
            var commands = new CapturingCommandFactory(throwOn: _ => null);
            var processor = BuildProcessor(commands);

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
            var processor = BuildProcessor(commands);

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
            await Assert.ThrowsAsync<InvalidOperationException>(() => manager.RegisterSocket(socket, session));

            // ...and the partial registration was rolled back: the presence key was released via a
            // compare-and-delete keyed on the exact socket id that was claimed (so a newer owner's key would be
            // left intact), on the player's presence key.
            var claim = Assert.Single(cache.GetSetWithExpiries);
            var release = Assert.Single(cache.CompareAndDeletes);
            Assert.Equal(claim.Key, release.Key);
            Assert.Equal(claim.Value, release.DeleteIfValue);
            Assert.Contains("42", release.Key);
        }

        private Func<IPubSubQueue, Task> BuildProcessor(CapturingCommandFactory commandFactory)
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
            manager.RegisterSocket(socket, session).GetAwaiter().GetResult();

            return capturingPubSub.CapturedProcessor
                ?? throw new InvalidOperationException("Processor was not registered.");
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

            public override AbstractSocketCommand CreateCommand(SocketCommandInfo commandInfo, IServiceScope scope)
            {
                ExecutedCommandIds.Add(commandInfo.Id ?? "<null>");
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

            public Task<T?> GetNextAsync<T>()
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

            public Task<string?> GetNextAsync() => throw new NotSupportedException();
            public string? GetNext() => throw new NotSupportedException();
            public T? GetNext<T>() => throw new NotSupportedException();
            public void AddToQueue(string value) => throw new NotSupportedException();
            public void AddToQueue<T>(T value) => throw new NotSupportedException();
            public Task AddToQueueAsync(string value) => throw new NotSupportedException();
            public Task AddToQueueAsync<T>(T value) => throw new NotSupportedException();
            public Task AddRangeToQueueAsync(IEnumerable<string> values) => throw new NotSupportedException();
        }

        private sealed class CapturingPubSubService : IPubSubService
        {
            public Func<IPubSubQueue, Task>? CapturedProcessor { get; private set; }

            public Task Subscribe(string channel, string queueName, Func<(IPubSubQueue queue, string channel), Task> action, string? id = null)
            {
                CapturedProcessor = queue => action((queue, channel));
                return Task.CompletedTask;
            }

            public Task Subscribe(string channel, string queueName, Action<(IPubSubQueue queue, string channel)> action, string? id = null) => Task.CompletedTask;
            public Task Subscribe(string channel, Action<(string message, string channel)> action, string? id = null) => Task.CompletedTask;
            public Task Publish(string channel, string message, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task Publish(string channel, string queueName, string queueData, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task Publish<T>(string channel, string queueName, T queueData, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task PublishBatch<T>(string channel, string queueName, IEnumerable<T> queueData, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task UnSubscribe(string channel) => Task.CompletedTask;
            public Task UnSubscribe(string channel, string id) => Task.CompletedTask;
            public IPubSubQueue GetQueue(string queueName) => throw new NotSupportedException();
        }

        // RegisterSocket only claims the presence key via the atomic GetSet-with-expiry; everything else is unused here.
        private sealed class NoOpCacheService : ICacheService
        {
            public Task<string?> GetSet(string key, string value, TimeSpan expiry, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

            public Task<string?> GetSet(string key, string value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task Expire(string key, TimeSpan expiry, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<string?> Get(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<T?> Get<T>(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<string?> GetDelete(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<T?> GetDelete<T>(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<T?> GetSet<T>(string key, T value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task Set(string key, string value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task Set<T>(string key, T value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task Set(string key, string value, TimeSpan expiry, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task Set<T>(string key, T value, TimeSpan expiry, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public void ExpireAndForget(string key, TimeSpan expiry) => throw new NotSupportedException();
            public void SetAndForget(string key, string value) => throw new NotSupportedException();
            public void SetAndForget<T>(string key, T value) => throw new NotSupportedException();
            public void SetAndForget(string key, string value, TimeSpan expiry) => throw new NotSupportedException();
            public void SetAndForget<T>(string key, T value, TimeSpan expiry) => throw new NotSupportedException();
            public Task SetNotExists(string key, string value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task Delete(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public void DeleteAndForget(string key) => throw new NotSupportedException();
            public Task CompareAndDelete(string key, string deleteIfValue, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        }

        /// <summary>A pub/sub whose <c>Subscribe</c> throws, to drive the registration-rollback path.</summary>
        private sealed class ThrowingSubscribePubSubService(Exception toThrow) : IPubSubService
        {
            public Task Subscribe(string channel, string queueName, Func<(IPubSubQueue queue, string channel), Task> action, string? id = null) => throw toThrow;

            public Task Subscribe(string channel, string queueName, Action<(IPubSubQueue queue, string channel)> action, string? id = null) => throw new NotSupportedException();
            public Task Subscribe(string channel, Action<(string message, string channel)> action, string? id = null) => throw new NotSupportedException();
            public Task Publish(string channel, string message, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task Publish(string channel, string queueName, string queueData, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task Publish<T>(string channel, string queueName, T queueData, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task PublishBatch<T>(string channel, string queueName, IEnumerable<T> queueData, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task UnSubscribe(string channel) => Task.CompletedTask;
            public Task UnSubscribe(string channel, string id) => Task.CompletedTask;
            public IPubSubQueue GetQueue(string queueName) => throw new NotSupportedException();
        }

        /// <summary>Records the presence-key writes and releases so a rollback test can assert the exact
        /// claimed socket id is the one compare-and-deleted.</summary>
        private sealed class RecordingCacheService : ICacheService
        {
            public List<(string Key, string? Value)> GetSetWithExpiries { get; } = [];
            public List<(string Key, string DeleteIfValue)> CompareAndDeletes { get; } = [];

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

            public Task<string?> GetSet(string key, string value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task Expire(string key, TimeSpan expiry, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<string?> Get(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<T?> Get<T>(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<string?> GetDelete(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<T?> GetDelete<T>(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<T?> GetSet<T>(string key, T value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task Set(string key, string value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task Set<T>(string key, T value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task Set(string key, string value, TimeSpan expiry, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task Set<T>(string key, T value, TimeSpan expiry, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public void ExpireAndForget(string key, TimeSpan expiry) => throw new NotSupportedException();
            public void SetAndForget(string key, string value) => throw new NotSupportedException();
            public void SetAndForget<T>(string key, T value) => throw new NotSupportedException();
            public void SetAndForget(string key, string value, TimeSpan expiry) => throw new NotSupportedException();
            public void SetAndForget<T>(string key, T value, TimeSpan expiry) => throw new NotSupportedException();
            public Task SetNotExists(string key, string value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task Delete(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public void DeleteAndForget(string key) => throw new NotSupportedException();
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
            public Task<PlayerState?> GetSession(int userId) => Task.FromResult<PlayerState?>(null);
            public void Update(PlayerState sessionData, int playerId) { }
            public void Clear(int userId) { }
        }

        private sealed class NoOpUnitOfWork : IUnitOfWork
        {
            public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
    }
}
