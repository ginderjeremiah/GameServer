using Game.Abstractions.DataAccess;
using Game.Api;
using Game.Api.Models.Common;
using Game.Api.Services;
using Game.Api.Sockets;
using Game.Api.Sockets.Commands;
using Game.Application;
using Game.Core.Players;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.WebSockets;
using System.Text.Json;
using Xunit;

namespace Game.Api.Tests.Unit
{
    /// <summary>
    /// Verifies how <see cref="SocketHandler"/> classifies a command's result (#671): the client read-loop
    /// path (<see cref="SocketHandler.ExecuteCommand"/>) surfaces a genuine fault to the awaiting client as an
    /// "Internal Server Error", while the server-push path (<see cref="SocketHandler.ExecuteServerCommand"/>)
    /// instead reports a <see cref="SocketCommandOutcome"/> so the pub/sub processor can escalate without an
    /// (unawaited) error response. Both treat a non-timeout cancellation as a lifetime/teardown unwind rather
    /// than a fault. A <see cref="FakeWebSocket"/> and hand-built dependencies keep these as plain unit tests.
    /// </summary>
    public sealed class SocketCommandExecutionTests : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILoggerFactory _loggerFactory;
        private readonly CapturingLoggerProvider _logs = new();

        public SocketCommandExecutionTests()
        {
            _provider = new ServiceCollection()
                .AddScoped<IUnitOfWork, NoOpUnitOfWork>()
                .BuildServiceProvider();
            _scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();
            _loggerFactory = LoggerFactory.Create(b => b.AddProvider(_logs).SetMinimumLevel(LogLevel.Trace));
        }

        [Fact]
        public async Task HandleMessage_FrameMissingName_RejectsWithMalformedErrorInsteadOfThrowing()
        {
            // A structurally-valid frame that omits "name" deserializes to a SocketCommandInfo with a null
            // Name. It must be rejected with a structured error the client can react to, not throw an
            // unobserved exception that leaves the client hanging on its request id (#935).
            var (socket, handler) = CreateHandler(_ => null);

            await handler.HandleMessage("{\"id\":\"c1\",\"parameters\":null}");

            Assert.Contains(socket.SentMessages, m => m.Contains("Malformed command.") && m.Contains("c1"));
            Assert.Contains(_logs.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("no name"));
        }

        [Fact]
        public async Task HandleMessage_UnknownCommandName_RejectsWithUnknownCommandErrorInsteadOfFaulting()
        {
            // A frame naming a command with no registered generator (a stale/garbage name) is a bad request,
            // not an internal fault: it must be rejected with a structured "Unknown command." error rather than
            // reaching the command lookup, whose throw would be logged at error and surfaced as an
            // "Internal Server Error".
            var (socket, handler) = CreateHandler(_ => null);

            await handler.HandleMessage("{\"id\":\"c1\",\"name\":\"Unknown\",\"parameters\":null}");

            Assert.Contains(socket.SentMessages, m => m.Contains("Unknown command.") && m.Contains("c1"));
            Assert.Contains(_logs.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("unknown socket command"));
            // It never reached the fault path: no error-level log, no "Internal Server Error" frame.
            Assert.DoesNotContain(_logs.Entries, e => e.Level == LogLevel.Error);
            Assert.DoesNotContain(socket.SentMessages, m => m.Contains("Internal Server Error"));
        }

        [Fact]
        public async Task ExecuteCommand_CommandFaults_SurfacesInternalServerErrorToTheClient()
        {
            var (socket, handler) = CreateHandler(name => name == "Boom" ? new InvalidOperationException("boom") : null);

            await handler.ExecuteCommand(new SocketCommandInfo("Boom") { Id = "c1" });

            Assert.Contains(socket.SentMessages, m => m.Contains("Internal Server Error") && m.Contains("c1"));
            Assert.Contains(_logs.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("c1"));
        }

        [Fact]
        public async Task ExecuteServerCommand_CommandFaults_ReturnsFaultedAndSendsNoErrorResponse()
        {
            var (socket, handler) = CreateHandler(_ => new InvalidOperationException("boom"));

            var outcome = await handler.ExecuteServerCommand(new SocketCommandInfo("Boom") { Id = "c1" });

            Assert.Equal(SocketCommandOutcome.Faulted, outcome);
            // A server push has no awaiting client request, so no "Internal Server Error" goes back — the
            // processor owns the surfacing (a ServerCommandFailed notice) instead.
            Assert.DoesNotContain(socket.SentMessages, m => m.Contains("Internal Server Error"));
            // The fault is still logged (not swallowed) so the failure is captured once.
            Assert.Contains(_logs.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("c1"));
        }

        [Fact]
        public async Task ExecuteCommand_LifetimeCancellation_TreatedAsTeardownNotInternalServerError()
        {
            // A non-timeout OperationCanceledException is a lifetime/teardown unwind, not a command defect:
            // the per-command timeout guard does not match it, so it must be logged as a teardown rather than
            // mislabelled "Internal Server Error" (the prior behaviour this issue fixes).
            var (socket, handler) = CreateHandler(_ => new OperationCanceledException("teardown"));

            await handler.ExecuteCommand(new SocketCommandInfo("Cancel") { Id = "c1" });

            Assert.DoesNotContain(socket.SentMessages, m => m.Contains("Internal Server Error"));
            Assert.Contains(_logs.Entries, e => e.Level == LogLevel.Debug && e.Message.Contains("cancelled during teardown"));
            Assert.DoesNotContain(_logs.Entries, e => e.Level == LogLevel.Error);
        }

        [Fact]
        public async Task ExecuteServerCommand_LifetimeCancellation_ReturnsTornDownWithoutEscalation()
        {
            var (_, handler) = CreateHandler(_ => new OperationCanceledException("teardown"));

            var outcome = await handler.ExecuteServerCommand(new SocketCommandInfo("Cancel") { Id = "c1" });

            Assert.Equal(SocketCommandOutcome.TornDown, outcome);
            Assert.DoesNotContain(_logs.Entries, e => e.Level == LogLevel.Error);
        }

        [Fact]
        public async Task ExecuteServerCommand_Succeeds_ReturnsSucceededAndSendsTheResponse()
        {
            var (socket, handler) = CreateHandler(_ => null);

            var outcome = await handler.ExecuteServerCommand(new SocketCommandInfo("Ok") { Id = "c1" });

            Assert.Equal(SocketCommandOutcome.Succeeded, outcome);
            Assert.Contains(socket.SentMessages, m => m.Contains("c1") && !m.Contains("Internal Server Error"));
        }

        [Fact]
        public async Task ExecuteCommand_ParametersMalformed_RejectsWithMalformedParametersInsteadOfInternalServerError()
        {
            // SetParameters (invoked from CreateCommand) throws on unparseable/missing Parameters JSON — a bad
            // request, not a server fault (#1498).
            var (socket, handler) = CreateHandler(_ => null, throwOnCreate: name => name == "BadParams" ? new MalformedSocketCommandParametersException(name, new JsonException("bad json")) : null);

            await handler.ExecuteCommand(new SocketCommandInfo("BadParams") { Id = "c1" });

            Assert.Contains(socket.SentMessages, m => m.Contains("Malformed parameters.") && m.Contains("c1"));
            Assert.DoesNotContain(socket.SentMessages, m => m.Contains("Internal Server Error"));
            // A bad request is a warning, not an error — it must not be logged (or surfaced) like a genuine fault.
            Assert.Contains(_logs.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("malformed parameters"));
            Assert.DoesNotContain(_logs.Entries, e => e.Level == LogLevel.Error);
        }

        [Fact]
        public async Task ExecuteServerCommand_ParametersMalformed_ReturnsMalformedParametersAndLogsForEscalation()
        {
            // A malformed server-push payload is server-authored, so it's a genuine bug — classified distinctly
            // from a client bad-request but still escalate-worthy, unlike ExecuteCommand's client-facing path.
            var (socket, handler) = CreateHandler(_ => null, throwOnCreate: name => name == "BadParams" ? new MalformedSocketCommandParametersException(name, new JsonException("bad json")) : null);

            var outcome = await handler.ExecuteServerCommand(new SocketCommandInfo("BadParams") { Id = "c1" });

            Assert.Equal(SocketCommandOutcome.MalformedParameters, outcome);
            Assert.DoesNotContain(socket.SentMessages, m => m.Contains("Internal Server Error"));
            Assert.Contains(_logs.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("c1"));
        }

        [Fact]
        public async Task ExecuteCommand_ResponseNotDelivered_LogsWarningWithoutTreatingItAsSucceededOrFaulted()
        {
            // The command runs fine, but the send itself fails (e.g. the socket closed mid-command) — must not
            // be silently reported as Succeeded (#1498).
            var (socket, handler) = CreateHandler(_ => null);
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, TestContext.Current.CancellationToken);

            await handler.ExecuteCommand(new SocketCommandInfo("Ok") { Id = "c1" });

            Assert.Empty(socket.SentMessages);
            Assert.DoesNotContain(socket.SentMessages, m => m.Contains("Internal Server Error"));
            Assert.Contains(_logs.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("Failed to deliver") && e.Message.Contains("c1"));
        }

        [Fact]
        public async Task ExecuteServerCommand_ResponseNotDelivered_ReturnsNotDeliveredAndLogsWarning()
        {
            var (socket, handler) = CreateHandler(_ => null);
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, TestContext.Current.CancellationToken);

            var outcome = await handler.ExecuteServerCommand(new SocketCommandInfo("Ok") { Id = "c1" });

            Assert.Equal(SocketCommandOutcome.NotDelivered, outcome);
            Assert.Contains(_logs.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("Failed to deliver"));
            // Not escalation-worthy: the payload itself was fine, only delivery failed.
            Assert.DoesNotContain(_logs.Entries, e => e.Level == LogLevel.Error);
        }

        [Fact]
        public async Task ExecuteServerCommand_SelfDeliveringCommand_ReturnsSucceededWithoutASecondSendOrDeliveryWarning()
        {
            // Mirrors SocketReplaced: the command sends its own response and closes the socket before
            // returning. The runner must not attempt (and then misclassify as NotDelivered) a follow-up send
            // on the now-closed socket (#1636).
            var (socket, handler) = CreateHandler(_ => null, selfDelivering: name => name == "SelfDelivers");

            var outcome = await handler.ExecuteServerCommand(new SocketCommandInfo("SelfDelivers") { Id = "c1" });

            Assert.Equal(SocketCommandOutcome.Succeeded, outcome);
            Assert.Single(socket.SentMessages, m => m.Contains("c1"));
            Assert.DoesNotContain(_logs.Entries, e => e.Message.Contains("Failed to deliver"));
        }

        [Fact]
        public async Task ExecuteCommand_SelfDeliveringCommand_DoesNotAttemptASecondSend()
        {
            var (socket, handler) = CreateHandler(_ => null, selfDelivering: name => name == "SelfDelivers");

            await handler.ExecuteCommand(new SocketCommandInfo("SelfDelivers") { Id = "c1" });

            Assert.Single(socket.SentMessages, m => m.Contains("c1"));
            Assert.DoesNotContain(_logs.Entries, e => e.Message.Contains("Failed to deliver"));
        }

        [Fact]
        public async Task HandleMessage_UnparseableJson_ClosesTheSocketInsteadOfLeavingTheClientToTimeOut()
        {
            // An invalid-JSON frame carries no request id to correlate a structured rejection to, so closing
            // (mirroring the oversized-frame path) lets the client's close handler surface the drop and
            // reconnect promptly instead of waiting out its 30s request timeout undiagnosed (#1498).
            var (socket, handler) = CreateHandler(_ => null);

            await handler.HandleMessage("{not valid json");

            Assert.True(socket.CloseAsyncCalled);
            Assert.Equal(WebSocketCloseStatus.InvalidPayloadData, socket.CloseStatusUsed);
            Assert.Contains(_logs.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("Failed to deserialize"));
        }

        private (FakeWebSocket Socket, SocketHandler Handler) CreateHandler(Func<string, Exception?> throwOn, Func<string, Exception?>? throwOnCreate = null, Func<string, bool>? selfDelivering = null)
        {
            var socket = new FakeWebSocket(sendDuration: TimeSpan.Zero);
            var session = new SessionService(new NoOpSessionStore());
            session.CreateSession(userId: 1, playerId: 1);
            var context = new SocketContext(socket, playerId: 1, session, isAdmin: false, _loggerFactory.CreateLogger<SocketContext>());
            var handler = new SocketHandler(context, new StubCommandFactory(throwOn, throwOnCreate, selfDelivering), _scopeFactory,
                _loggerFactory.CreateLogger<SocketHandler>(), () => { });
            return (socket, handler);
        }

        public void Dispose()
        {
            _loggerFactory.Dispose();
            _provider.Dispose();
        }

        /// <summary>A command factory that produces a command which either succeeds or throws the exception
        /// the supplied selector returns for the command name. <paramref name="throwOnCreate"/> optionally
        /// throws directly from <see cref="CreateCommand"/> instead — simulating what the real factory's
        /// <c>SetParameters</c> call already throws (a classified <see cref="MalformedSocketCommandParametersException"/>)
        /// once parameter binding fails, before a command is ever fully constructed.</summary>
        private sealed class StubCommandFactory(Func<string, Exception?> throwOn, Func<string, Exception?>? throwOnCreate = null, Func<string, bool>? selfDelivering = null) : SocketCommandFactory
        {
            public override AbstractSocketCommand CreateCommand(SocketCommandInfo commandInfo, IServiceScope scope)
            {
                if (throwOnCreate?.Invoke(commandInfo.Name) is { } creationFault)
                {
                    throw creationFault;
                }

                if (selfDelivering?.Invoke(commandInfo.Name) is true)
                {
                    return new SelfDeliveringStubCommand(commandInfo.Name) { Id = commandInfo.Id };
                }

                return new StubCommand(commandInfo.Name, throwOn(commandInfo.Name)) { Id = commandInfo.Id };
            }

            // Every name is treated as registered except the explicit "Unknown" sentinel, so the
            // HandleMessage unknown-command guard can be exercised without populating the static registry.
            public override bool IsKnownCommand(string commandName) => commandName != "Unknown";
        }

        private sealed class StubCommand(string name, Exception? toThrow) : AbstractSocketCommand
        {
            public override string Name { get; set; } = name;

            public override Task<ApiSocketResponse> ExecuteAsync(SocketContext context, CancellationToken cancellationToken)
            {
                if (toThrow is not null)
                {
                    throw toThrow;
                }

                return Task.FromResult(Success());
            }
        }

        /// <summary>Mirrors SocketReplaced's send-then-close-then-return-Success shape for #1636 coverage.</summary>
        private sealed class SelfDeliveringStubCommand(string name) : AbstractSocketCommand, ISelfDeliveringCommand
        {
            public override string Name { get; set; } = name;

            public override async Task<ApiSocketResponse> ExecuteAsync(SocketContext context, CancellationToken cancellationToken)
            {
                await context.SendData(Success());
                await context.Close(ESocketCloseReason.SocketReplaced);
                return Success();
            }
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
