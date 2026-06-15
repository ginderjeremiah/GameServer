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
        public async Task Processor_EmptyQueue_CompletesWithoutDispatchingOrThrowing()
        {
            var commands = new CapturingCommandFactory(throwOn: _ => null);
            var processor = BuildProcessor(commands);

            await processor(new FakeQueue());

            Assert.Empty(commands.ExecutedCommandIds);
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

        private sealed class FakeQueue(params SocketCommandInfo[] commands) : IPubSubQueue
        {
            private readonly Queue<SocketCommandInfo> _commands = new(commands);

            public Task<T?> GetNextAsync<T>()
            {
                if (_commands.TryDequeue(out var next) && next is T typed)
                {
                    return Task.FromResult<T?>(typed);
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

        // RegisterSocket only touches GetSet + Expire on the presence key; everything else is unused here.
        private sealed class NoOpCacheService : ICacheService
        {
            public Task<string?> GetSet(string key, string value, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
            public Task Expire(string key, TimeSpan expiry, CancellationToken cancellationToken = default) => Task.CompletedTask;

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
