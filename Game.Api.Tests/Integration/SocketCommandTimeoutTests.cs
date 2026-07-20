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
        // Long enough that a cold JIT/connection-pool run (e.g. this class run in isolation, with no prior
        // test in the process having warmed the EF/Npgsql code paths the `Next`/`Coop` commands' own
        // scope-creation and SaveChangesAsync round trip through) doesn't spuriously blow the budget itself —
        // short enough that the deliberately-wedged `Hung`/`Coop` commands below still reliably time out well
        // within WaitTimeout (#2184).
        private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(750);
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
        public async Task CooperativelyCancellingCommand_MarksPlayerForReload()
        {
            using var scope = CreateScope();
            // This command parks on a gate that is never released, but it honors the cancellation token — the
            // same shape RunCommand's SavePlayer would take if a dispatch/flush observed the per-command budget
            // mid-save. An unhandled OperationCanceledException settles the abandoned task as Canceled rather
            // than Faulted, so the release continuation must classify that as a stranded-mutation risk too (#1849).
            var neverReleased = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var session = scope.ServiceProvider.GetRequiredService<SessionService>();
            var (socket, handler, _) = CreateHandler(scope, ShortTimeout,
                new GatedCommand("Coop", neverReleased.Task, observeToken: true));

            await handler.ExecuteCommand(new SocketCommandInfo("Coop") { Id = "coop-1" });
            await WaitForSentMessageAsync(socket, m => m.Contains("Command timed out"));

            // The abandoned task settles asynchronously once it observes the cancelled token; poll for the
            // release continuation to have run and marked the session.
            var markedForReload = await PollingHelper.PollUntilAsync(
                () => Task.FromResult(session.PlayerNeedsReload),
                satisfied: marked => marked);
            Assert.True(markedForReload);
        }

        [Fact]
        public async Task CommandThatThrowsCancellationUnrelatedToTheBudget_IsTornDown_AndMarksPlayerForReload()
        {
            using var scope = CreateScope();
            // Simulates a leaked dependency cancellation (e.g. an internal token unrelated to the per-command
            // budget) surfacing before the budget elapses: cts.IsCancellationRequested is false, so this misses
            // RunCommandUnderLock's timeout-specific catch and reaches its outer "teardown" catch instead — which
            // must also treat it as a possible stranded mutation, not assume nothing was mutated (#1849).
            var session = scope.ServiceProvider.GetRequiredService<SessionService>();
            var (socket, handler, scopeFactory) = CreateHandler(scope, WaitTimeout,
                new GatedCommand("LeakedCancel", Task.CompletedTask, observeToken: false, faultWith: new OperationCanceledException("simulated leaked cancellation")));

            await handler.ExecuteCommand(new SocketCommandInfo("LeakedCancel") { Id = "leaked-1" });

            Assert.True(session.PlayerNeedsReload);
            Assert.Equal(1, scopeFactory.ScopesCreated);
            Assert.DoesNotContain(socket.SentMessages, m => m.Contains("leaked-1"));
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
            var context = new SocketContext(socket, playerId: 1, session, isAdmin: false, contextLogger);
            var handler = new SocketHandler(context, commandFactory, scopeFactory, handlerLogger,
                () => { }, commandTimeout);

            return (socket, handler, scopeFactory);
        }

        private static async Task WaitForSentMessageAsync(FakeWebSocket socket, Func<string, bool> predicate)
        {
            var found = await PollingHelper.PollUntilAsync(
                () => Task.FromResult(socket.SentMessages.Any(predicate)), sent => sent, (int)WaitTimeout.TotalMilliseconds);

            Assert.True(found, "Timed out waiting for the expected message to be sent.");
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
