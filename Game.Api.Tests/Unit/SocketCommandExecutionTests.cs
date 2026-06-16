using Game.Abstractions.DataAccess;
using Game.Api.Models.Common;
using Game.Api.Services;
using Game.Api.Sockets;
using Game.Api.Sockets.Commands;
using Game.Application;
using Game.Core.Players;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

        private (FakeWebSocket Socket, SocketHandler Handler) CreateHandler(Func<string, Exception?> throwOn)
        {
            var socket = new FakeWebSocket(sendDuration: TimeSpan.Zero);
            var session = new SessionService(new NoOpSessionStore());
            session.CreateSession(userId: 1, playerId: 1);
            var context = new SocketContext(socket, playerId: 1, session, _loggerFactory.CreateLogger<SocketContext>());
            var handler = new SocketHandler(context, new StubCommandFactory(throwOn), _scopeFactory,
                _loggerFactory.CreateLogger<SocketHandler>(), () => Task.CompletedTask);
            return (socket, handler);
        }

        public void Dispose()
        {
            _loggerFactory.Dispose();
            _provider.Dispose();
        }

        /// <summary>A command factory that produces a command which either succeeds or throws the exception
        /// the supplied selector returns for the command name.</summary>
        private sealed class StubCommandFactory(Func<string, Exception?> throwOn) : SocketCommandFactory
        {
            public override AbstractSocketCommand CreateCommand(SocketCommandInfo commandInfo, IServiceScope scope)
            {
                return new StubCommand(commandInfo.Name, throwOn(commandInfo.Name)) { Id = commandInfo.Id };
            }
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
