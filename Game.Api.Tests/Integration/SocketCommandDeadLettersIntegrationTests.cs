using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.Infrastructure;
using Game.Api;
using Game.Api.Models.Progress;
using Game.Api.Services.Admin;
using Game.Api.Sockets.Commands;
using Game.Core;
using Game.TestInfrastructure.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Api.Tests.Integration
{
    /// <summary>
    /// Covers the guarded socket command dead-letter inspection/replay surface (#1542): a read-only peek that
    /// classifies each entry (malformed / unknown command / not-replayable session-lifecycle command /
    /// replayable) and derives the addressed player id, and a replay that redelivers to whatever socket is
    /// currently live for that player rather than the (possibly long-gone) socket the command originally
    /// failed on — leaving an entry queued, never lost, when no addressable or currently-live player exists,
    /// or when the entry is a session-lifecycle command that is never safe to redeliver later (#1854).
    /// Exercised against real Redis since it is a thin adapter over an out-of-process queue and the
    /// socket-presence cache.
    /// </summary>
    [Collection("Integration")]
    public class SocketCommandDeadLettersIntegrationTests : ApiIntegrationTestBase
    {
        public SocketCommandDeadLettersIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        [Fact]
        public async Task InspectAsync_EmptyQueue_ReportsZeroDepthAndNoEntries()
        {
            using var scope = CreateScope();
            var deadLetters = scope.ServiceProvider.GetRequiredService<SocketCommandDeadLetters>();

            var inspection = await deadLetters.InspectAsync(50);

            Assert.Equal(0, inspection.TotalCount);
            Assert.Empty(inspection.Entries);
        }

        [Fact]
        public async Task InspectAsync_ClassifiesEachEntry_AndDerivesCommandNameAndPlayerId()
        {
            using var scope = CreateScope();
            await SeedSocketDeadLettersAsync(
                "this-is-not-json",
                SocketDeadLetterEnvelope(99, new SocketCommandInfo("MysteryCommand")),
                SocketDeadLetterEnvelope(42, ChallengeCompletedEnvelope()));

            var deadLetters = scope.ServiceProvider.GetRequiredService<SocketCommandDeadLetters>();
            var inspection = await deadLetters.InspectAsync(50);

            Assert.Equal(3, inspection.TotalCount);
            Assert.Equal(3, inspection.Entries.Count);

            // Entries come back head-first, in the order they were dead-lettered.
            var malformed = inspection.Entries[0];
            Assert.Equal(0, malformed.Index);
            Assert.Equal(EDeadLetterReason.Malformed, malformed.Reason);
            Assert.Null(malformed.EventType);
            Assert.Null(malformed.PlayerId);

            var unknown = inspection.Entries[1];
            Assert.Equal(1, unknown.Index);
            Assert.Equal(EDeadLetterReason.UnknownEventType, unknown.Reason);
            Assert.Equal("MysteryCommand", unknown.EventType);
            Assert.Equal(99, unknown.PlayerId);

            var replayable = inspection.Entries[2];
            Assert.Equal(2, replayable.Index);
            Assert.Equal(EDeadLetterReason.Replayable, replayable.Reason);
            Assert.Equal(nameof(ChallengeCompleted), replayable.EventType);
            Assert.Equal(42, replayable.PlayerId);
        }

        [Fact]
        public async Task InspectAsync_ClassifiesASessionLifecycleCommandAsNotReplayable()
        {
            using var scope = CreateScope();
            // SocketReplaced is server-initiated (a known command) but only ever meaningful at the moment it
            // was originally emitted — closing whatever socket happens to be live for the player later would
            // kick an innocent session, so it must classify distinctly from a genuinely Replayable entry.
            await SeedSocketDeadLettersAsync(SocketDeadLetterEnvelope(42, new SocketReplacedInfo()));

            var deadLetters = scope.ServiceProvider.GetRequiredService<SocketCommandDeadLetters>();
            var inspection = await deadLetters.InspectAsync(50);

            var entry = Assert.Single(inspection.Entries);
            Assert.Equal(EDeadLetterReason.NotReplayable, entry.Reason);
            Assert.Equal(nameof(SocketReplaced), entry.EventType);
            Assert.Equal(42, entry.PlayerId);
        }

        [Fact]
        public async Task InspectAsync_RespectsMax_WhileStillReportingFullDepth()
        {
            using var scope = CreateScope();
            await SeedSocketDeadLettersAsync(
                SocketDeadLetterEnvelope(1, ChallengeCompletedEnvelope()),
                SocketDeadLetterEnvelope(2, ChallengeCompletedEnvelope()),
                SocketDeadLetterEnvelope(3, ChallengeCompletedEnvelope()));

            var deadLetters = scope.ServiceProvider.GetRequiredService<SocketCommandDeadLetters>();
            var inspection = await deadLetters.InspectAsync(2);

            // The page is capped at the requested max, but the reported depth is the full queue.
            Assert.Equal(3, inspection.TotalCount);
            Assert.Equal(2, inspection.Entries.Count);
            Assert.Equal([1, 2], inspection.Entries.Select(e => e.PlayerId));
        }

        [Fact]
        public async Task ReplayAllAsync_RedeliversToEachPlayersCurrentLiveSocket_AndEmptiesTheDeadLetterQueue()
        {
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
            const int playerA = 101;
            const int playerB = 102;
            var socketA = Guid.NewGuid().ToString();
            var socketB = Guid.NewGuid().ToString();
            await cache.Set(PresenceKey(playerA), socketA, TimeSpan.FromMinutes(1));
            await cache.Set(PresenceKey(playerB), socketB, TimeSpan.FromMinutes(1));

            await SeedSocketDeadLettersAsync(
                SocketDeadLetterEnvelope(playerA, ChallengeCompletedEnvelope()),
                SocketDeadLetterEnvelope(playerB, ChallengeCompletedEnvelope()));

            var deadLetters = scope.ServiceProvider.GetRequiredService<SocketCommandDeadLetters>();
            var result = await deadLetters.ReplayAllAsync();

            Assert.Equal(2, result.ReplayedCount);
            Assert.Equal(0, result.RemainingCount);

            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            Assert.Equal(0, await pubsub.GetQueue(Constants.PUBSUB_SOCKET_DEAD_LETTER_QUEUE).GetLengthAsync());
            // Redelivered onto each player's CURRENT socket queue, not a queue keyed by the original socket.
            Assert.Single(await pubsub.GetQueue(SocketQueueName(socketA)).PeekAsync(10));
            Assert.Single(await pubsub.GetQueue(SocketQueueName(socketB)).PeekAsync(10));
        }

        [Fact]
        public async Task ReplaySelectedAsync_MovesOnlyTheChosenEntry_LeavingTheRest()
        {
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
            const int targetPlayer = 201;
            var socketId = Guid.NewGuid().ToString();
            await cache.Set(PresenceKey(targetPlayer), socketId, TimeSpan.FromMinutes(1));

            var keep = SocketDeadLetterEnvelope(202, ChallengeCompletedEnvelope());
            var replay = SocketDeadLetterEnvelope(targetPlayer, ChallengeCompletedEnvelope());
            await SeedSocketDeadLettersAsync(keep, replay);

            var deadLetters = scope.ServiceProvider.GetRequiredService<SocketCommandDeadLetters>();
            var result = await deadLetters.ReplaySelectedAsync([replay]);

            Assert.Equal(1, result.ReplayedCount);
            Assert.Equal(1, result.RemainingCount);

            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            Assert.Equal([keep], await pubsub.GetQueue(Constants.PUBSUB_SOCKET_DEAD_LETTER_QUEUE).PeekAsync(10));
            Assert.Single(await pubsub.GetQueue(SocketQueueName(socketId)).PeekAsync(10));
        }

        [Fact]
        public async Task ReplaySelectedAsync_IgnoresPayloadsNotOnTheQueue_NeverInjectingThemOntoALiveSocket()
        {
            using var scope = CreateScope();
            var real = SocketDeadLetterEnvelope(301, ChallengeCompletedEnvelope());
            await SeedSocketDeadLettersAsync(real);

            var deadLetters = scope.ServiceProvider.GetRequiredService<SocketCommandDeadLetters>();
            // A fabricated payload that was never dead-lettered must be skipped, so replay can't be used to
            // push an arbitrary command onto a player's socket.
            var result = await deadLetters.ReplaySelectedAsync(["{\"playerId\":999,\"command\":{\"name\":\"Injected\"}}"]);

            Assert.Equal(0, result.ReplayedCount);
            Assert.Equal(1, result.RemainingCount);

            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            Assert.Equal([real], await pubsub.GetQueue(Constants.PUBSUB_SOCKET_DEAD_LETTER_QUEUE).PeekAsync(10));
        }

        [Fact]
        public async Task ReplayAllAsync_SkipsEntriesWithNoAddressableOrCurrentlyLivePlayer_LeavingThemQueued()
        {
            using var scope = CreateScope();
            var malformed = "not-json";
            // Well-formed and known, but the player has no live socket (no presence key seeded) — there is
            // nowhere to redeliver it, so it must stay on the dead-letter queue rather than being dropped.
            var noLiveSocket = SocketDeadLetterEnvelope(401, ChallengeCompletedEnvelope());
            await SeedSocketDeadLettersAsync(malformed, noLiveSocket);

            var deadLetters = scope.ServiceProvider.GetRequiredService<SocketCommandDeadLetters>();
            var result = await deadLetters.ReplayAllAsync();

            Assert.Equal(0, result.ReplayedCount);
            Assert.Equal(2, result.RemainingCount);

            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            Assert.Equal([malformed, noLiveSocket], await pubsub.GetQueue(Constants.PUBSUB_SOCKET_DEAD_LETTER_QUEUE).PeekAsync(10));
        }

        [Fact]
        public async Task ReplayAllAsync_LeavesAnUnknownCommandEntryQueued_EvenWithALiveSocket()
        {
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
            const int playerId = 501;
            var socketId = Guid.NewGuid().ToString();
            await cache.Set(PresenceKey(playerId), socketId, TimeSpan.FromMinutes(1));

            // Well-formed, addressed to a player with a live socket right now — but the command name isn't a
            // registered server-initiated command (e.g. dead-lettered, then renamed/removed in a later
            // deploy). Replaying it would ship a command the client can't recognize and, unlike the player
            // write-behind queue, there is no re-processing round-trip that lets it come back — so it must
            // stay queued rather than being delivered and dropped.
            var unknown = SocketDeadLetterEnvelope(playerId, new SocketCommandInfo("RetiredCommand"));
            await SeedSocketDeadLettersAsync(unknown);

            var deadLetters = scope.ServiceProvider.GetRequiredService<SocketCommandDeadLetters>();
            var result = await deadLetters.ReplayAllAsync();

            Assert.Equal(0, result.ReplayedCount);
            Assert.Equal(1, result.RemainingCount);

            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            Assert.Equal([unknown], await pubsub.GetQueue(Constants.PUBSUB_SOCKET_DEAD_LETTER_QUEUE).PeekAsync(10));
            Assert.Empty(await pubsub.GetQueue(SocketQueueName(socketId)).PeekAsync(10));
        }

        [Fact]
        public async Task ReplayAllAsync_LeavesASessionLifecycleEntryQueued_EvenWithALiveSocket()
        {
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
            const int playerId = 601;
            var socketId = Guid.NewGuid().ToString();
            await cache.Set(PresenceKey(playerId), socketId, TimeSpan.FromMinutes(1));

            // A dead-lettered SocketReplaced is only meaningful at the moment it was originally emitted —
            // replaying it later would force-close whatever socket is currently live for the player, which is
            // never correct. It must stay queued even though the player has a live socket right now.
            var sessionLifecycle = SocketDeadLetterEnvelope(playerId, new SocketReplacedInfo());
            await SeedSocketDeadLettersAsync(sessionLifecycle);

            var deadLetters = scope.ServiceProvider.GetRequiredService<SocketCommandDeadLetters>();
            var result = await deadLetters.ReplayAllAsync();

            Assert.Equal(0, result.ReplayedCount);
            Assert.Equal(1, result.RemainingCount);

            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            Assert.Equal([sessionLifecycle], await pubsub.GetQueue(Constants.PUBSUB_SOCKET_DEAD_LETTER_QUEUE).PeekAsync(10));
            Assert.Empty(await pubsub.GetQueue(SocketQueueName(socketId)).PeekAsync(10));
        }

        [Fact]
        public async Task InspectAsync_ClassifiesANullCommandEnvelopeAsMalformed_WithoutThrowing()
        {
            using var scope = CreateScope();
            // `required` only guarantees the "command" key is present, not that its value is non-null — a
            // payload like this deserializes without error but must still classify as Malformed rather than
            // NRE-ing out of InspectAsync.
            await SeedSocketDeadLettersAsync("{\"playerId\":5,\"command\":null}");

            var deadLetters = scope.ServiceProvider.GetRequiredService<SocketCommandDeadLetters>();
            var inspection = await deadLetters.InspectAsync(50);

            var entry = Assert.Single(inspection.Entries);
            Assert.Equal(EDeadLetterReason.Malformed, entry.Reason);
            Assert.Null(entry.EventType);
            Assert.Null(entry.PlayerId);
        }

        /// <summary>A stand-in for a genuinely replayable server-initiated push in these tests.</summary>
        private static ChallengeCompletedInfo ChallengeCompletedEnvelope() => new(new ChallengeCompletedModel { ChallengeId = 1 });
    }
}
