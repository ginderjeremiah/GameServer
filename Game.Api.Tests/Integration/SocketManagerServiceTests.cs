using Game.Abstractions.Infrastructure;
using Game.Api.Services;
using Game.Api.Sockets.Commands;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.WebSockets;
using Xunit;

namespace Game.Api.Tests.Integration
{
    /// <summary>
    /// Integration coverage for the socket-presence lifecycle that backs the documented single-active-
    /// connection / session-takeover model (see <c>docs/backend.md</c> → "Single active connection").
    /// <see cref="SocketManagerService"/> is Redis-coupled and holds no unit-worthy logic, so its
    /// presence-key contract is exercised here against the real cache fixture.
    /// </summary>
    [Collection("Integration")]
    public class SocketManagerServiceTests : ApiIntegrationTestBase
    {
        // Field initializer runs before the base constructor so the provider is ready when
        // CreateFactory is invoked from the base constructor (mirrors RequestLoggingTests).
        private readonly CapturingLoggerProvider _capturingProvider = new();

        private static readonly string SocketManagerCategory = typeof(SocketManagerService).FullName!;

        public SocketManagerServiceTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        protected override GameServerFactory CreateFactory(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
        {
            return new GameServerFactory(containers, testOutputHelper, [_capturingProvider]);
        }

        [Fact]
        public async Task RegisterSocket_SecondConnection_ReplacesPresenceKeyAndNotifiesOldSocket()
        {
            var (userId, playerId) = await SeedAndLoginAsync();

            // First connection registers and owns the presence key.
            await using var socketA = new TestSocketClient();
            await socketA.ConnectAsync(Factory.Server.CreateWebSocketClient(), userId);
            await socketA.SendCommandAsync<object>("GetStatisticTypes");
            var idA = await ReadPresenceValueAsync(playerId);
            Assert.NotNull(idA);
            Assert.True(await HasActiveSocketAsync(playerId));

            // Second connection takes over: the old socket is told it was replaced...
            await using var socketB = new TestSocketClient();
            await socketB.ConnectAsync(Factory.Server.CreateWebSocketClient(), userId);

            // SocketReplaced is emitted with no command Id, so it surfaces as a null-Id server push.
            var replaced = await socketA.WaitForResponseAsync(null);
            Assert.Equal(nameof(SocketReplaced), replaced.Name);

            // ...the superseded connection is actually closed, not just notified (#1959): dropping or
            // reordering the Close() call inside SocketReplaced.ExecuteAsync would leave two live sockets
            // for one player — the exact condition the single-connection model exists to prevent — with
            // every assertion above this one still passing.
            var (closeStatus, closeDescription) = await socketA.WaitForCloseAsync();
            Assert.Equal(WebSocketState.Closed, socketA.State);
            Assert.Equal(WebSocketCloseStatus.NormalClosure, closeStatus);
            Assert.Equal(ESocketCloseReason.SocketReplaced.GetDescription(), closeDescription);

            // ...and the presence key now points at the new socket rather than the old one.
            await socketB.SendCommandAsync<object>("GetStatisticTypes");
            var idB = await ReadPresenceValueAsync(playerId);
            Assert.NotNull(idB);
            Assert.NotEqual(idA, idB);
        }

        [Fact]
        public async Task RegisterSocket_DifferentCharacterOnSameAccount_ClosesTheOtherCharactersSocketAndClaimsTheAccountSlot()
        {
            // The scenario #1817 flags: an account has two characters, and a second tab/device selects and
            // connects a *different* character without the first ever tearing its own socket down (a stale
            // tab, or a client that skips the switch flow). Since Session_{userId} is keyed by account, both
            // sockets would otherwise keep clobbering each other's cached in-flight battle state with no
            // signal to either side. This must be kicked exactly like a same-character takeover.
            var (userId, playerId1) = await SeedAndLoginAsync();
            var playerId2 = await SeedSecondPlayerAsync(userId);

            await using var socketA = new TestSocketClient();
            await socketA.ConnectAsync(Factory.Server.CreateWebSocketClient(), userId, playerId1);
            await socketA.SendCommandAsync<object>("GetStatisticTypes");
            Assert.Equal(playerId1.ToString(), await ReadAccountPresenceValueAsync(userId));

            await using var socketB = new TestSocketClient();
            await socketB.ConnectAsync(Factory.Server.CreateWebSocketClient(), userId, playerId2);

            var replaced = await socketA.WaitForResponseAsync(null);
            Assert.Equal(nameof(SocketReplaced), replaced.Name);
            var (closeStatus, closeDescription) = await socketA.WaitForCloseAsync();
            Assert.Equal(WebSocketState.Closed, socketA.State);
            Assert.Equal(WebSocketCloseStatus.NormalClosure, closeStatus);
            Assert.Equal(ESocketCloseReason.SocketReplaced.GetDescription(), closeDescription);

            await socketB.SendCommandAsync<object>("GetStatisticTypes");
            Assert.Equal(playerId2.ToString(), await ReadAccountPresenceValueAsync(userId));
        }

        [Fact]
        public async Task RegisterSocket_SameCharacterReconnect_DoesNotAlsoTriggerAnAccountLevelReplace()
        {
            // A same-character reconnect (the existing per-player takeover) must not also be mistaken for a
            // different-character account-level takeover and send a second, stray SocketReplaced.
            var (userId, playerId) = await SeedAndLoginAsync();

            await using var socketA = new TestSocketClient();
            await socketA.ConnectAsync(Factory.Server.CreateWebSocketClient(), userId, playerId);
            await socketA.SendCommandAsync<object>("GetStatisticTypes");

            await using var socketB = new TestSocketClient();
            await socketB.ConnectAsync(Factory.Server.CreateWebSocketClient(), userId, playerId);

            // If a spurious second push had been queued ahead of the close frame, this read would return it
            // instead of a close frame and the assertions below would fail.
            var replaced = await socketA.WaitForResponseAsync(null);
            Assert.Equal(nameof(SocketReplaced), replaced.Name);
            var (closeStatus, _) = await socketA.WaitForCloseAsync();
            Assert.Equal(WebSocketState.Closed, socketA.State);
            Assert.Equal(WebSocketCloseStatus.NormalClosure, closeStatus);
        }

        [Fact]
        public async Task UnRegisterSocket_GracefulClose_ClearsAccountPresenceKey()
        {
            var (userId, playerId) = await SeedAndLoginAsync();

            await using var socketA = new TestSocketClient();
            await socketA.ConnectAsync(Factory.Server.CreateWebSocketClient(), userId, playerId);
            await socketA.SendCommandAsync<object>("GetStatisticTypes");
            Assert.Equal(playerId.ToString(), await ReadAccountPresenceValueAsync(userId));

            await socketA.CloseAsync();

            Assert.True(await WaitUntilAsync(async () => await ReadAccountPresenceValueAsync(userId) is null),
                "Expected the account presence key to be cleared after the socket closed gracefully.");
        }

        [Fact]
        public async Task UnRegisterSocket_ReplacedByDifferentCharacter_DoesNotClearTheNewAccountPresenceKey()
        {
            var (userId, playerId1) = await SeedAndLoginAsync();
            var playerId2 = await SeedSecondPlayerAsync(userId);

            await using var socketA = new TestSocketClient();
            await socketA.ConnectAsync(Factory.Server.CreateWebSocketClient(), userId, playerId1);
            await socketA.SendCommandAsync<object>("GetStatisticTypes");

            await using var socketB = new TestSocketClient();
            await socketB.ConnectAsync(Factory.Server.CreateWebSocketClient(), userId, playerId2);

            var replaced = await socketA.WaitForResponseAsync(null);
            Assert.Equal(nameof(SocketReplaced), replaced.Name);
            await socketB.SendCommandAsync<object>("GetStatisticTypes");
            Assert.Equal(playerId2.ToString(), await ReadAccountPresenceValueAsync(userId));

            // Character 1's own teardown is a compare-and-delete keyed on its own player id, so it must not
            // clear the account slot character 2 now owns.
            await Task.Delay(1000, CancellationToken);
            Assert.Equal(playerId2.ToString(), await ReadAccountPresenceValueAsync(userId));
            var stillAlive = await socketB.SendCommandAsync<object>("GetStatisticTypes");
            Assert.Null(stillAlive.Error);
        }

        [Fact]
        public async Task RefreshSocketPresence_AfterAccountKeyLapsesWhileStillLive_ResurrectsItsOwnClaim()
        {
            var (userId, playerId) = await SeedAndLoginAsync();
            var key = AccountPresenceKey(userId);

            await using var socketA = new TestSocketClient();
            await socketA.ConnectAsync(Factory.Server.CreateWebSocketClient(), userId, playerId);
            await socketA.SendCommandAsync<object>("GetStatisticTypes");
            Assert.Equal(playerId.ToString(), await ReadAccountPresenceValueAsync(userId));

            // Simulate the account presence key lapsing out from under a still-live socket, mirroring
            // RefreshSocketPresence_AfterItsKeyLapsesWhileStillLive_ResurrectsItsOwnClaim for the per-player key.
            using (var scope = CreateScope())
            {
                var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
                await cache.Delete(key);
            }
            Assert.Null(await ReadAccountPresenceValueAsync(userId));

            await socketA.SendCommandAsync<object>("GetStatisticTypes");

            Assert.True(await WaitUntilAsync(async () => await ReadAccountPresenceValueAsync(userId) == playerId.ToString()),
                "Expected the live socket's next heartbeat to resurrect its own account-level claim.");
        }

        [Fact]
        public async Task UnRegisterSocket_GracefulClose_ClearsPresenceKey()
        {
            var (userId, playerId) = await SeedAndLoginAsync();

            await using var socketA = new TestSocketClient();
            await socketA.ConnectAsync(Factory.Server.CreateWebSocketClient(), userId);
            await socketA.SendCommandAsync<object>("GetStatisticTypes");
            Assert.True(await HasActiveSocketAsync(playerId));

            // A clean close drives the server through UnRegisterSocket, which compare-and-deletes the key.
            await socketA.CloseAsync();

            Assert.True(await WaitUntilAsync(async () => !await HasActiveSocketAsync(playerId)),
                "Expected the presence key to be cleared after the socket closed gracefully.");
        }

        [Fact]
        public async Task UnRegisterSocket_ReplacedSocket_DoesNotDeleteNewSocketPresenceKey()
        {
            var (userId, playerId) = await SeedAndLoginAsync();

            await using var socketA = new TestSocketClient();
            await socketA.ConnectAsync(Factory.Server.CreateWebSocketClient(), userId);
            await socketA.SendCommandAsync<object>("GetStatisticTypes");

            await using var socketB = new TestSocketClient();
            await socketB.ConnectAsync(Factory.Server.CreateWebSocketClient(), userId);

            // Wait for the takeover to land, then capture the new owner of the key.
            var replaced = await socketA.WaitForResponseAsync(null);
            Assert.Equal(nameof(SocketReplaced), replaced.Name);
            await socketB.SendCommandAsync<object>("GetStatisticTypes");
            var idB = await ReadPresenceValueAsync(playerId);
            Assert.NotNull(idB);

            // The replaced socket's own teardown (UnRegisterSocket) is a compare-and-delete keyed on its
            // stale socket id, so it must not delete the key the newer connection has taken over. Give the
            // server-side cleanup time to run, then prove the key still belongs to the surviving socket.
            await Task.Delay(1000, CancellationToken);
            Assert.Equal(idB, await ReadPresenceValueAsync(playerId));
            var stillAlive = await socketB.SendCommandAsync<object>("GetStatisticTypes");
            Assert.Null(stillAlive.Error);
        }

        [Fact]
        public async Task RefreshSocketPresence_FromConnectionThatWasTakenOver_ProlongsCurrentKeyWithoutResurrectingOldSocketId()
        {
            var (userId, playerId) = await SeedAndLoginAsync();
            var key = PresenceKey(playerId);

            // Register a real socket so its inbound activity drives the refresh path.
            await using var socketA = new TestSocketClient();
            await socketA.ConnectAsync(Factory.Server.CreateWebSocketClient(), userId);
            await socketA.SendCommandAsync<object>("GetStatisticTypes");
            var idA = await ReadPresenceValueAsync(playerId);
            Assert.NotNull(idA);

            // Simulate a newer connection taking over the key (exactly what a second RegisterSocket does),
            // but keep socket A open so it can still send the refreshing activity. A short TTL lets the
            // subsequent extend-TTL be observed.
            var newerSocketId = Guid.NewGuid().ToString();
            using (var scope = CreateScope())
            {
                var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
                await cache.Set(key, newerSocketId, TimeSpan.FromSeconds(8));
            }
            await Task.Delay(500, CancellationToken);

            // Inbound activity on the old connection refreshes presence via Expire (not a re-set). The
            // read loop refreshes before handling the message, so the command's response confirms it ran.
            await socketA.SendCommandAsync<object>("GetStatisticTypes");

            // The refresh prolonged whatever key was current — it did not re-set the old socket id.
            Assert.Equal(newerSocketId, await ReadPresenceValueAsync(playerId));
            var refreshedTtl = await GetPresenceTtlAsync(playerId);
            Assert.NotNull(refreshedTtl);
            Assert.True(refreshedTtl > TimeSpan.FromSeconds(12),
                $"Expected the TTL to be extended past its 8s ceiling but it was {refreshedTtl}.");
        }

        [Fact]
        public async Task RefreshSocketPresence_AfterItsKeyLapsesWhileStillLive_ResurrectsItsOwnClaim()
        {
            var (userId, playerId) = await SeedAndLoginAsync();
            var key = PresenceKey(playerId);

            await using var socketA = new TestSocketClient();
            await socketA.ConnectAsync(Factory.Server.CreateWebSocketClient(), userId);
            await socketA.SendCommandAsync<object>("GetStatisticTypes");
            var idA = await ReadPresenceValueAsync(playerId);
            Assert.NotNull(idA);

            // Simulate the presence key lapsing out from under a still-live socket — an inbound stall past the
            // TTL, or a rolled-back superseding registration — by deleting it directly (#1497).
            using (var scope = CreateScope())
            {
                var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
                await cache.Delete(key);
            }
            Assert.Null(await ReadPresenceValueAsync(playerId));

            // The next heartbeat on the still-live socket must resurrect its own claim rather than leaving the
            // key permanently gone (a bare ExpireAndForget is a no-op on a missing key and never recovers).
            await socketA.SendCommandAsync<object>("GetStatisticTypes");

            Assert.True(await WaitUntilAsync(async () => await ReadPresenceValueAsync(playerId) == idA),
                "Expected the live socket's next heartbeat to resurrect its own presence claim.");
        }

        [Fact]
        public async Task HasActiveSocket_AfterPresenceTtlExpires_ReturnsFalse()
        {
            // HasActiveSocket is a pure presence-key read, so a directly-seeded key with a short TTL
            // exercises the expiry contract without waiting out the production window.
            const int playerId = 987654;
            var key = PresenceKey(playerId);

            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
            var socketManager = scope.ServiceProvider.GetRequiredService<SocketManagerService>();

            await cache.Set(key, Guid.NewGuid().ToString(), TimeSpan.FromSeconds(1));
            Assert.True(await socketManager.HasActiveSocket(playerId));

            Assert.True(await WaitUntilAsync(async () => !await socketManager.HasActiveSocket(playerId)),
                "Expected HasActiveSocket to report false once the presence key TTL expired.");
        }

        [Fact]
        public async Task TryClaimForSwitchCredit_KeyUnset_ClaimsAndReturnsTrue()
        {
            const int playerId = 765432;

            using var scope = CreateScope();
            var socketManager = scope.ServiceProvider.GetRequiredService<SocketManagerService>();

            Assert.True(await socketManager.TryClaimForSwitchCredit(playerId));
            var claimed = await ReadPresenceValueAsync(playerId);
            Assert.NotNull(claimed);
            // The claim isn't a real socket id: HasActiveSocket still reports it as present (see the doc
            // comment on TryClaimForSwitchCredit) since the key itself is what a genuine takeover checks.
            Assert.True(await socketManager.HasActiveSocket(playerId));
        }

        [Fact]
        public async Task TryClaimForSwitchCredit_ActiveSocketAlreadyPresent_ReturnsFalseAndLeavesItIntact()
        {
            var (userId, playerId) = await SeedAndLoginAsync("switchcreditlive", "switchcreditlivepass");

            await using var socketA = new TestSocketClient();
            await socketA.ConnectAsync(Factory.Server.CreateWebSocketClient(), userId);
            await socketA.SendCommandAsync<object>("GetStatisticTypes");
            var realSocketId = await ReadPresenceValueAsync(playerId);
            Assert.NotNull(realSocketId);

            using var scope = CreateScope();
            var socketManager = scope.ServiceProvider.GetRequiredService<SocketManagerService>();

            Assert.False(await socketManager.TryClaimForSwitchCredit(playerId));
            Assert.Equal(realSocketId, await ReadPresenceValueAsync(playerId));
        }

        [Fact]
        public async Task ReleaseSwitchCreditClaim_StillOwned_ClearsTheKey()
        {
            const int playerId = 654321;

            using var scope = CreateScope();
            var socketManager = scope.ServiceProvider.GetRequiredService<SocketManagerService>();

            Assert.True(await socketManager.TryClaimForSwitchCredit(playerId));
            await socketManager.ReleaseSwitchCreditClaim(playerId);

            Assert.Null(await ReadPresenceValueAsync(playerId));
        }

        [Fact]
        public async Task ReleaseSwitchCreditClaim_KeyTakenOverByARealSocket_DoesNotClearIt()
        {
            var (userId, playerId) = await SeedAndLoginAsync("switchcreditkicked", "switchcreditkickedpass");

            using var scope = CreateScope();
            var socketManager = scope.ServiceProvider.GetRequiredService<SocketManagerService>();
            Assert.True(await socketManager.TryClaimForSwitchCredit(playerId));

            // A real connection lands (and kicks the claim) while the credit that claimed it is still running.
            await using var socketA = new TestSocketClient();
            await socketA.ConnectAsync(Factory.Server.CreateWebSocketClient(), userId, playerId);
            await socketA.SendCommandAsync<object>("GetStatisticTypes");
            var realSocketId = await ReadPresenceValueAsync(playerId);
            Assert.NotNull(realSocketId);

            // The credit's later release must be a no-op — releasing unconditionally would delete the newer
            // connection's presence key out from under it.
            await socketManager.ReleaseSwitchCreditClaim(playerId);
            Assert.Equal(realSocketId, await ReadPresenceValueAsync(playerId));
        }

        [Fact]
        public async Task RegisterSocket_SwitchCreditClaimActive_DefersUntilClaimReleases()
        {
            var (userId, playerId) = await SeedAndLoginAsync("switchcreditdefer", "switchcreditdeferpass");

            using var scope = CreateScope();
            var socketManager = scope.ServiceProvider.GetRequiredService<SocketManagerService>();

            // Simulate an in-flight switch-away credit (#2041) holding the presence key before a new
            // connection for the same player arrives.
            Assert.True(await socketManager.TryClaimForSwitchCredit(playerId));
            var claimedValue = await ReadPresenceValueAsync(playerId);
            Assert.NotNull(claimedValue);

            // The WebSocket handshake itself completes (accepting the socket happens before RegisterSocket
            // runs), but the server won't start processing commands on it until RegisterSocket completes -
            // so a command's response only arrives once the defer clears.
            await using var socketA = new TestSocketClient();
            await socketA.ConnectAsync(Factory.Server.CreateWebSocketClient(), userId, playerId);
            var commandTask = socketA.SendCommandAsync<object>("GetStatisticTypes");

            await Task.Delay(300, CancellationToken);
            Assert.False(commandTask.IsCompleted,
                "Expected the new connection to defer registration while the switch-credit claim is held.");
            Assert.Equal(claimedValue, await ReadPresenceValueAsync(playerId));

            await socketManager.ReleaseSwitchCreditClaim(playerId);

            var completed = await Task.WhenAny(commandTask, Task.Delay(5000, CancellationToken));
            Assert.Same(commandTask, completed);
            Assert.Null((await commandTask).Error);
            Assert.NotEqual(claimedValue, await ReadPresenceValueAsync(playerId));
        }

        [Fact]
        public async Task EmitSocketCommand_ByPlayerId_SwitchCreditClaimActive_ReportsNoActiveSocketRatherThanPublishingToTheClaim()
        {
            // A resolved switch-credit claim isn't a socket to publish to (#2076 review) — reporting it as
            // one would let a caller like the dead-letter replay believe a push was delivered when it was
            // actually published into a queue nothing drains.
            const int playerId = 543210;
            var startIndex = _capturingProvider.Entries.Count;

            using var scope = CreateScope();
            var socketManager = scope.ServiceProvider.GetRequiredService<SocketManagerService>();
            Assert.True(await socketManager.TryClaimForSwitchCredit(playerId));

            var delivered = await socketManager.EmitSocketCommand(new SocketCommandInfo("GetStatisticTypes"), playerId);

            Assert.False(delivered);
            var warning = Assert.Single(
                _capturingProvider.Entries.Skip(startIndex),
                e => e.Category == SocketManagerCategory && e.Level == LogLevel.Warning);
            Assert.Contains("no active socket", warning.Message);
            Assert.Equal(playerId, warning.Properties.Single(p => p.Key == "PlayerId").Value);
        }

        [Fact]
        public async Task EmitSocketCommand_NoActiveSocket_LogsWarning()
        {
            // No presence key exists for this player, so EmitSocketCommand has no socket to publish to: it
            // takes the warn-and-skip branch rather than publishing.
            const int playerId = 876543;
            var startIndex = _capturingProvider.Entries.Count;

            using (var scope = CreateScope())
            {
                var socketManager = scope.ServiceProvider.GetRequiredService<SocketManagerService>();
                await socketManager.EmitSocketCommand(new SocketCommandInfo("GetStatisticTypes"), playerId);
            }

            var warning = Assert.Single(
                _capturingProvider.Entries.Skip(startIndex),
                e => e.Category == SocketManagerCategory && e.Level == LogLevel.Warning);
            Assert.Contains("no active socket", warning.Message);
            Assert.Equal(playerId, warning.Properties.Single(p => p.Key == "PlayerId").Value);
        }

        [Fact]
        public async Task RevokeAccess_PlayerHasActiveSocket_PushesAccessRevokedAndCloses()
        {
            var (userId, playerId) = await SeedAndLoginAsync();

            await using var socketA = new TestSocketClient();
            await socketA.ConnectAsync(Factory.Server.CreateWebSocketClient(), userId);
            await socketA.SendCommandAsync<object>("GetStatisticTypes");
            Assert.True(await HasActiveSocketAsync(playerId));

            using (var scope = CreateScope())
            {
                var socketManager = scope.ServiceProvider.GetRequiredService<SocketManagerService>();
                await socketManager.RevokeAccess([playerId]);
            }

            // AccessRevoked is emitted with no command Id, so it surfaces as a null-Id server push, mirroring
            // how RegisterSocket_SecondConnection_ReplacesPresenceKeyAndNotifiesOldSocket observes SocketReplaced.
            var revoked = await socketA.WaitForResponseAsync(null);
            Assert.Equal(nameof(AccessRevoked), revoked.Name);

            var (closeStatus, closeDescription) = await socketA.WaitForCloseAsync();
            Assert.Equal(WebSocketState.Closed, socketA.State);
            Assert.Equal(WebSocketCloseStatus.NormalClosure, closeStatus);
            Assert.Equal(ESocketCloseReason.AccessRevoked.GetDescription(), closeDescription);
        }

        [Fact]
        public async Task RevokeAccess_PlayerHasNoActiveSocket_IsSilentNoOp()
        {
            // Ban/archive targets are very often offline, unlike the gameplay pushes EmitSocketCommand's
            // no-active-socket branch is meant to warn about — RevokeAccess must not emit that warning for
            // what is, here, the common case (see the doc comment on RevokeAccess).
            const int playerId = 192837;
            var startIndex = _capturingProvider.Entries.Count;

            using (var scope = CreateScope())
            {
                var socketManager = scope.ServiceProvider.GetRequiredService<SocketManagerService>();
                await socketManager.RevokeAccess([playerId]);
            }

            Assert.DoesNotContain(_capturingProvider.Entries.Skip(startIndex),
                e => e.Category == SocketManagerCategory && e.Level == LogLevel.Warning);
        }

        [Fact]
        public async Task RevokeAccess_MixOfLiveAndOfflinePlayerIds_OnlyClosesTheLiveOne()
        {
            var (userId, playerId) = await SeedAndLoginAsync();
            const int offlinePlayerId = 283746;

            await using var socketA = new TestSocketClient();
            await socketA.ConnectAsync(Factory.Server.CreateWebSocketClient(), userId);
            await socketA.SendCommandAsync<object>("GetStatisticTypes");

            using (var scope = CreateScope())
            {
                var socketManager = scope.ServiceProvider.GetRequiredService<SocketManagerService>();
                await socketManager.RevokeAccess([offlinePlayerId, playerId]);
            }

            var revoked = await socketA.WaitForResponseAsync(null);
            Assert.Equal(nameof(AccessRevoked), revoked.Name);
        }

        private async Task<bool> HasActiveSocketAsync(int playerId)
        {
            using var scope = CreateScope();
            var socketManager = scope.ServiceProvider.GetRequiredService<SocketManagerService>();
            return await socketManager.HasActiveSocket(playerId);
        }

        private async Task<string?> ReadPresenceValueAsync(int playerId)
        {
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
            return await cache.Get(PresenceKey(playerId));
        }

        private async Task<string?> ReadAccountPresenceValueAsync(int userId)
        {
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
            return await cache.Get(AccountPresenceKey(userId));
        }

        /// <summary>Polls the condition until it holds or a short timeout elapses; returns whether it held.</summary>
        private static Task<bool> WaitUntilAsync(Func<Task<bool>> condition, int timeoutMs = 5000) =>
            PollingHelper.PollUntilAsync(condition, held => held, timeoutMs);
    }
}
