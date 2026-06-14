using Game.Api.Models.Common;
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
    /// Verifies the per-command timeout that stops a wedged socket command from holding the per-socket
    /// command lock — and thus the player's whole command stream — indefinitely (#483). A
    /// <see cref="FakeWebSocket"/> stands in for the transport and a gated test command stands in for one
    /// stuck on a slow/dead dependency; the rest of the services are resolved from the real host. The
    /// timeout must surface a response while still preserving the serialization guarantee the command lock
    /// provides (see <c>docs/backend.md</c> → "HTTP vs WebSocket Communication").
    /// </summary>
    [Collection("Integration")]
    public class SocketCommandTimeoutTests : ApiIntegrationTestBase
    {
        private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(200);
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        public SocketCommandTimeoutTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task WedgedCommand_TimesOutWithResponse_AndHoldsLockUntilItSettles()
        {
            using var scope = CreateScope();
            var hungGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var (socket, handler, scopeFactory) = CreateHandler(scope, ShortTimeout,
                new GatedCommand("Hung", hungGate.Task, observeToken: false),
                new GatedCommand("Next", Task.CompletedTask, observeToken: false));

            var first = handler.ExecuteCommand(new SocketCommandInfo("Hung") { Id = "hung-1" });

            // The command never observes cancellation, so the budget elapses and it is abandoned with a
            // timeout response so the read/pub-sub loop isn't left hanging.
            await WaitForSentMessageAsync(socket, m => m.Contains("Command timed out"));
            Assert.Equal(1, scopeFactory.ScopesCreated);

            // The lock is still held by the abandoned task, so a second command cannot even open its work
            // scope — the next command would otherwise race the wedged one on the shared cached player.
            var second = handler.ExecuteCommand(new SocketCommandInfo("Next") { Id = "next-1" });
            await Task.Delay(300, CancellationToken);
            Assert.Equal(1, scopeFactory.ScopesCreated);

            // Once the abandoned command finally settles, the lock releases and the queued command runs.
            hungGate.SetResult();
            await first;
            await second.WaitAsync(WaitTimeout, CancellationToken);
            Assert.Equal(2, scopeFactory.ScopesCreated);
            Assert.Contains(socket.SentMessages, m => m.Contains("next-1") && !m.Contains("timed out"));

            // The abandoned command commits but its now-late response is suppressed: the client only ever
            // saw the timeout for hung-1, never a second (success) response for the same id.
            var hungResponses = socket.SentMessages.Where(m => m.Contains("hung-1")).ToList();
            Assert.Single(hungResponses);
            Assert.Contains("timed out", hungResponses[0]);
        }

        [Fact]
        public async Task CooperativelyCancellingCommand_TimesOut_AndReleasesLockSoNextCommandRuns()
        {
            using var scope = CreateScope();
            // This command parks on a gate that is never released, but it honors the cancellation token.
            var neverReleased = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var (socket, handler, scopeFactory) = CreateHandler(scope, ShortTimeout,
                new GatedCommand("Coop", neverReleased.Task, observeToken: true),
                new GatedCommand("Next", Task.CompletedTask, observeToken: false));

            var first = handler.ExecuteCommand(new SocketCommandInfo("Coop") { Id = "coop-1" });
            await WaitForSentMessageAsync(socket, m => m.Contains("Command timed out"));

            // Because the command unwound cooperatively when the budget cancelled the token, the lock is
            // released without any external signal and the next command runs to completion on its own.
            var second = handler.ExecuteCommand(new SocketCommandInfo("Next") { Id = "next-1" });
            await Task.WhenAll(first, second).WaitAsync(WaitTimeout, CancellationToken);
            Assert.Equal(2, scopeFactory.ScopesCreated);
            Assert.Contains(socket.SentMessages, m => m.Contains("next-1") && !m.Contains("timed out"));
        }

        [Fact]
        public async Task CommandThatFinishesWithinBudget_CompletesNormally_WithoutTimeout()
        {
            using var scope = CreateScope();
            var (socket, handler, scopeFactory) = CreateHandler(scope, WaitTimeout,
                new GatedCommand("Fast", Task.CompletedTask, observeToken: false));

            await handler.ExecuteCommand(new SocketCommandInfo("Fast") { Id = "fast-1" })
                .WaitAsync(WaitTimeout, CancellationToken);

            Assert.Equal(1, scopeFactory.ScopesCreated);
            Assert.Contains(socket.SentMessages, m => m.Contains("fast-1"));
            Assert.DoesNotContain(socket.SentMessages, m => m.Contains("timed out"));
        }

        [Fact]
        public async Task AbandonedCommandThatLaterFaults_StillReleasesLock_SoNextCommandRuns()
        {
            using var scope = CreateScope();
            var faultGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var (socket, handler, scopeFactory) = CreateHandler(scope, ShortTimeout,
                new GatedCommand("Faulty", faultGate.Task, observeToken: false, faultWith: new InvalidOperationException("boom")),
                new GatedCommand("Next", Task.CompletedTask, observeToken: false));

            var first = handler.ExecuteCommand(new SocketCommandInfo("Faulty") { Id = "faulty-1" });
            await WaitForSentMessageAsync(socket, m => m.Contains("Command timed out"));

            var second = handler.ExecuteCommand(new SocketCommandInfo("Next") { Id = "next-1" });
            await Task.Delay(300, CancellationToken);
            Assert.Equal(1, scopeFactory.ScopesCreated);

            // The abandoned command faults after the timeout; the release continuation observes the fault
            // (so it isn't an unobserved exception) and still releases the lock, so the queued command runs.
            faultGate.SetResult();
            await first;
            await second.WaitAsync(WaitTimeout, CancellationToken);
            Assert.Equal(2, scopeFactory.ScopesCreated);
            Assert.Contains(socket.SentMessages, m => m.Contains("next-1") && !m.Contains("timed out"));
        }

        private (FakeWebSocket Socket, SocketHandler Handler, CountingServiceScopeFactory ScopeFactory) CreateHandler(
            IServiceScope scope, TimeSpan commandTimeout, params GatedCommand[] gatedCommands)
        {
            var session = scope.ServiceProvider.GetRequiredService<SessionService>();
            var contextLogger = scope.ServiceProvider.GetRequiredService<ILogger<SocketContext>>();
            var handlerLogger = scope.ServiceProvider.GetRequiredService<ILogger<SocketHandler>>();
            var scopeFactory = new CountingServiceScopeFactory(scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>());

            var commandFactory = new GatingSocketCommandFactory(gatedCommands.ToDictionary(c => c.Name));
            var socket = new FakeWebSocket(sendDuration: TimeSpan.Zero);
            var context = new SocketContext(socket, playerId: 1, session, contextLogger);
            var handler = new SocketHandler(context, commandFactory, scopeFactory, handlerLogger,
                () => Task.CompletedTask, commandTimeout);

            return (socket, handler, scopeFactory);
        }

        private async Task WaitForSentMessageAsync(FakeWebSocket socket, Func<string, bool> predicate)
        {
            var deadline = DateTime.UtcNow + WaitTimeout;
            while (DateTime.UtcNow < deadline)
            {
                if (socket.SentMessages.Any(predicate))
                {
                    return;
                }

                await Task.Delay(20, CancellationToken);
            }

            Assert.Fail("Timed out waiting for the expected message to be sent.");
        }

        /// <summary>
        /// A socket command whose execution parks on a supplied gate, standing in for one stuck on a slow
        /// dependency. When <c>observeToken</c> is set it parks cooperatively (honoring the cancellation
        /// token); otherwise it ignores the token, modelling a command wedged on a non-cancellable wait.
        /// </summary>
        private sealed class GatedCommand(string name, Task gate, bool observeToken, Exception? faultWith = null) : AbstractSocketCommand
        {
            public override string Name { get; set; } = name;

            public override async Task<ApiSocketResponse> ExecuteAsync(SocketContext context, CancellationToken cancellationToken)
            {
                if (observeToken)
                {
                    await gate.WaitAsync(cancellationToken);
                }
                else
                {
                    await gate;
                }

                if (faultWith is not null)
                {
                    throw faultWith;
                }

                return Success();
            }
        }

        private sealed class GatingSocketCommandFactory(IReadOnlyDictionary<string, GatedCommand> commands) : SocketCommandFactory
        {
            public override AbstractSocketCommand CreateCommand(SocketCommandInfo commandInfo, IServiceScope scope)
            {
                var command = commands[commandInfo.Name];
                command.Id = commandInfo.Id;
                return command;
            }
        }
    }
}
