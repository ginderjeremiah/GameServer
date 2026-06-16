using Game.Abstractions.DataAccess;
using Game.Api.Services;
using Game.Api.Sockets;
using Game.Api.Sockets.Commands;
using Game.Application;
using Game.Core.Players;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Game.Api.Tests.Unit
{
    /// <summary>
    /// Deterministically verifies the per-socket serialization that makes the documented "websocket
    /// commands are handled sequentially for each player" guarantee hold for server-initiated (pub/sub)
    /// commands as well as read-loop commands (see <c>docs/backend.md</c> → "HTTP vs WebSocket
    /// Communication"). A <see cref="FakeWebSocket"/> stands in for the transport — the in-memory test-host
    /// transport tolerates overlapping sends, so it cannot surface this race. These are pure serialization
    /// tests: they drive <see cref="SocketContext"/>/<see cref="SocketHandler"/> directly and depend on no
    /// out-of-process resource, so they run as plain unit tests with hand-built dependencies (the executed
    /// <c>GetStatisticTypes</c> command returns intrinsic reference data and never touches the DB/Redis).
    /// #478, #504.
    /// </summary>
    public class SocketSerializationTests
    {
        private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

        [Fact]
        public async Task SendData_ConcurrentCalls_NeverOverlapWebSocketSends()
        {
            // WebSocket.SendAsync forbids overlapping sends, and the read loop, the pub/sub processor, and
            // the ping/close paths can all reach SendData, so it must serialize them itself.
            var session = new SessionService(new NoOpSessionStore());
            var socket = new FakeWebSocket();
            var context = new SocketContext(socket, playerId: 1, session, NullLogger<SocketContext>.Instance);

            var sends = Enumerable.Range(0, 10).Select(i => context.SendData($"message-{i}")).ToArray();
            var results = await Task.WhenAll(sends);

            Assert.All(results, Assert.True);
            Assert.Equal(10, socket.CompletedSends);
            Assert.Equal(1, socket.MaxConcurrentSends);
        }

        [Fact]
        public async Task ExecuteCommand_ConcurrentReadLoopAndPubSubPaths_RunOneAtATime()
        {
            // Both the read loop and the pub/sub processor call ExecuteCommand on the same handler; the
            // command lock must serialize them so a server push cannot read-modify-write the cached player
            // concurrently with a battle-completion command. Serialization is observed by parking the first
            // command inside its send and proving the second cannot even open its work scope until the
            // first releases.
            SocketCommandFactory.RegisterSocketCommandGenerators();

            // A minimal in-memory provider: the executed GetStatisticTypes command needs no dependency, but
            // SocketHandler.RunCommand commits an IUnitOfWork from each work scope.
            await using var provider = new ServiceCollection()
                .AddScoped<IUnitOfWork, NoOpUnitOfWork>()
                .BuildServiceProvider();

            var session = new SessionService(new NoOpSessionStore());
            var commandFactory = new SocketCommandFactory();
            var countingScopeFactory = new CountingServiceScopeFactory(
                provider.GetRequiredService<IServiceScopeFactory>());

            var sendGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var socket = new FakeWebSocket(sendGate.Task);
            var context = new SocketContext(socket, playerId: 1, session, NullLogger<SocketContext>.Instance);
            var handler = new SocketHandler(context, commandFactory, countingScopeFactory, NullLogger<SocketHandler>.Instance, () => Task.CompletedTask);

            var first = handler.ExecuteCommand(new SocketCommandInfo("GetStatisticTypes") { Id = "first" });
            var second = handler.ExecuteCommand(new SocketCommandInfo("GetStatisticTypes") { Id = "second" });

            // The first command is now parked inside its send. Give the second ample time to proceed if it
            // could — under the command lock it cannot open its scope until the first command releases.
            await socket.FirstSendStarted;
            await Task.Delay(250, CancellationToken);
            Assert.Equal(1, countingScopeFactory.ScopesCreated);

            // Release the first send; the second may now run, and the two must still never overlap a send.
            sendGate.SetResult();
            await Task.WhenAll(first, second);

            Assert.Equal(2, countingScopeFactory.ScopesCreated);
            Assert.Equal(1, socket.MaxConcurrentSends);
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
