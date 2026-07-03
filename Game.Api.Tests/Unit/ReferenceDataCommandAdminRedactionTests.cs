using Game.Abstractions.Contracts;
using Game.Abstractions.DataAccess;
using Game.Api.Services;
using Game.Api.Sockets;
using Game.Api.Sockets.Commands;
using Game.Core.Players;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net.WebSockets;
using Xunit;

namespace Game.Api.Tests.Unit
{
    /// <summary>
    /// Pins the admin-only projection (#1439): a reference-data command redacts
    /// <see cref="IHasDesignerNotes.DesignerNotes"/> to <c>""</c> for a non-admin socket connection but serves
    /// it unredacted to an admin one (the Workbench authors it over this same command), while the content
    /// version hash — always computed from the genuine, unredacted data — never depends on who's asking.
    /// </summary>
    public class ReferenceDataCommandAdminRedactionTests
    {
        private sealed class NotedModel : IHasDesignerNotes
        {
            public required int Id { get; init; }
            public required string DesignerNotes { get; set; }
        }

        private sealed record PlainModel(int Id, string Name);

        // Mirrors a real Get* command's mapper: GetReferenceData() maps fresh model instances from an
        // immutable source per call, never a shared cached instance, so mutating a served response can't
        // leak into a later call (see AbstractReferenceDataCommand.RedactAuthoringOnlyFields).
        private sealed class FakeNotedCommand : AbstractReferenceDataCommand<NotedModel>
        {
            private object _snapshot = new();
            private (int Id, string Notes)[] _source = [];

            public override string Name { get; set; } = nameof(FakeNotedCommand);

            public void Swap(params (int Id, string Notes)[] source)
            {
                _snapshot = new object();
                _source = source;
            }

            protected override IEnumerable<NotedModel> GetReferenceData()
            {
                return _source.Select(s => new NotedModel { Id = s.Id, DesignerNotes = s.Notes }).ToList();
            }

            protected override object VersionKey => _snapshot;
        }

        private sealed class FakePlainCommand : AbstractReferenceDataCommand<PlainModel>
        {
            private PlainModel[] _data = [];

            public override string Name { get; set; } = nameof(FakePlainCommand);

            public void Swap(params PlainModel[] data) => _data = data;

            protected override IEnumerable<PlainModel> GetReferenceData() => _data;

            protected override object VersionKey { get; } = new();
        }

        private static SocketContext CreateContext(bool isAdmin)
        {
            var session = new SessionService(new NoOpSessionStore());
            return new SocketContext(new NullWebSocket(), playerId: 1, session, isAdmin, NullLogger<SocketContext>.Instance);
        }

        [Fact]
        public async Task HandleExecuteAsync_NonAdminConnection_RedactsDesignerNotes()
        {
            var command = new FakeNotedCommand();
            command.Swap((0, "Why this piece exists."));

            var response = await command.HandleExecuteAsync(CreateContext(isAdmin: false), CancellationToken.None);

            var model = Assert.Single(response.Data);
            Assert.Equal("", model.DesignerNotes);
        }

        [Fact]
        public async Task HandleExecuteAsync_AdminConnection_PreservesDesignerNotes()
        {
            var command = new FakeNotedCommand();
            command.Swap((0, "Why this piece exists."));

            var response = await command.HandleExecuteAsync(CreateContext(isAdmin: true), CancellationToken.None);

            var model = Assert.Single(response.Data);
            Assert.Equal("Why this piece exists.", model.DesignerNotes);
        }

        [Fact]
        public async Task HandleExecuteAsync_ModelWithoutDesignerNotes_IsUnaffectedByRedaction()
        {
            var command = new FakePlainCommand();
            command.Swap(new PlainModel(0, "Alpha"));

            var response = await command.HandleExecuteAsync(CreateContext(isAdmin: false), CancellationToken.None);

            Assert.Equal("Alpha", Assert.Single(response.Data).Name);
        }

        [Fact]
        public async Task ComputeVersion_AfterNonAdminRedaction_StillHashesTheUnredactedData()
        {
            var command = new FakeNotedCommand();
            command.Swap((0, "Why this piece exists."));

            // Serve a redacted response to a non-admin connection first...
            await command.HandleExecuteAsync(CreateContext(isAdmin: false), CancellationToken.None);
            var version = command.ComputeVersion();

            // ...the memoized version must still equal hashing the genuine data: editing the note bumps the
            // hash once for every client (admin or not), even though the field itself never reaches a
            // non-admin response.
            var expected = ReferenceDataVersioning.ComputeVersion(new[] { new NotedModel { Id = 0, DesignerNotes = "Why this piece exists." } });
            Assert.Equal(expected, version);
        }

        [Fact]
        public async Task ComputeVersion_MemoizedBeforeNonAdminRedaction_IsUnaffectedByIt()
        {
            var command = new FakeNotedCommand();
            command.Swap((0, "Why this piece exists."));

            // The loading screen's real ordering: GetReferenceDataVersions (ComputeVersion) runs before a
            // client ever fetches the set itself.
            var beforeVersion = command.ComputeVersion();
            await command.HandleExecuteAsync(CreateContext(isAdmin: false), CancellationToken.None);
            var afterVersion = command.ComputeVersion();

            Assert.Equal(beforeVersion, afterVersion);
        }

        private sealed class NullWebSocket : WebSocket
        {
            public override WebSocketState State => WebSocketState.Open;
            public override WebSocketCloseStatus? CloseStatus => null;
            public override string? CloseStatusDescription => null;
            public override string? SubProtocol => null;
            public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) => throw new NotSupportedException();
            public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) => throw new NotSupportedException();
            public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => throw new NotSupportedException();
            public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => throw new NotSupportedException();
            public override void Abort() { }
            public override void Dispose() { }
        }

        private sealed class NoOpSessionStore : ISessionStore
        {
            public Task<PlayerState?> GetSession(int userId, CancellationToken cancellationToken = default) => Task.FromResult<PlayerState?>(null);
            public void Update(PlayerState sessionData, int playerId) { }
            public void Clear(int userId) { }
        }
    }
}
