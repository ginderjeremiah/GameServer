using Game.Api.Services;
using Game.Api.Sockets;
using Game.Api.Sockets.Commands;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Game.Api.Tests.Integration
{
    /// <summary>
    /// Deterministically verifies the per-socket serialization that makes the documented "websocket
    /// commands are handled sequentially for each player" guarantee hold for server-initiated (pub/sub)
    /// commands as well as read-loop commands (see <c>docs/backend.md</c> → "HTTP vs WebSocket
    /// Communication"). A <see cref="FakeWebSocket"/> stands in for the transport — the in-memory test-host
    /// transport tolerates overlapping sends, so it cannot surface this race — while the rest of the
    /// services are resolved from the real host. #478.
    /// </summary>
    [Collection("Integration")]
    public class SocketSerializationTests : ApiIntegrationTestBase
    {
        public SocketSerializationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task SendData_ConcurrentCalls_NeverOverlapWebSocketSends()
        {
            // WebSocket.SendAsync forbids overlapping sends, and the read loop, the pub/sub processor, and
            // the ping/close paths can all reach SendData, so it must serialize them itself.
            using var scope = CreateScope();
            var session = scope.ServiceProvider.GetRequiredService<SessionService>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<SocketContext>>();
            var socket = new FakeWebSocket();
            var context = new SocketContext(socket, playerId: 1, session, logger);

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

            using var scope = CreateScope();
            var session = scope.ServiceProvider.GetRequiredService<SessionService>();
            var contextLogger = scope.ServiceProvider.GetRequiredService<ILogger<SocketContext>>();
            var handlerLogger = scope.ServiceProvider.GetRequiredService<ILogger<SocketHandler>>();
            var commandFactory = scope.ServiceProvider.GetRequiredService<SocketCommandFactory>();
            var countingScopeFactory = new CountingServiceScopeFactory(
                scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>());

            var sendGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var socket = new FakeWebSocket(sendGate.Task);
            var context = new SocketContext(socket, playerId: 1, session, contextLogger);
            var handler = new SocketHandler(context, commandFactory, countingScopeFactory, handlerLogger, () => Task.CompletedTask);

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
    }
}
