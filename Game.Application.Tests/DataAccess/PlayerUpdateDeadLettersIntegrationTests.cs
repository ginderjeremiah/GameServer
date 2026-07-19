using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Abstractions.Infrastructure;
using Game.Core;
using Game.Core.Events;
using Game.Core.Players.Events;
using Game.DataAccess;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Covers the guarded dead-letter inspection/replay surface (#794): a read-only peek that classifies
    /// each entry (malformed / unknown type / replayable) and derives its event type and player id, and a
    /// replay that moves entries back onto the player update queue without ever losing one (push to the
    /// destination before removing from the dead-letter queue) or injecting a payload that was not actually
    /// dead-lettered. Exercised against real Redis since it is a thin adapter over an out-of-process queue.
    /// </summary>
    [Collection("Integration")]
    public class PlayerUpdateDeadLettersIntegrationTests : ApplicationIntegrationTestBase
    {
        public PlayerUpdateDeadLettersIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        [Fact]
        public async Task InspectAsync_EmptyQueue_ReportsZeroDepthAndNoEntries()
        {
            using var scope = CreateScope();
            var deadLetters = scope.ServiceProvider.GetRequiredService<IPlayerUpdateDeadLetters>();

            var inspection = await deadLetters.InspectAsync(50);

            Assert.Equal(0, inspection.TotalCount);
            Assert.Empty(inspection.Entries);
        }

        [Fact]
        public async Task InspectAsync_ClassifiesEachEntry_AndDerivesEventTypeAndPlayerId()
        {
            using var scope = CreateScope();
            await SeedDeadLettersAsync(scope,
                "this-is-not-json",
                Envelope("MysteryEvent", "{\"playerId\":99}"),
                EnvelopeFor(new ItemUnlockedEvent(PlayerId: 42, ItemId: 7)));

            var deadLetters = scope.ServiceProvider.GetRequiredService<IPlayerUpdateDeadLetters>();
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
            Assert.Equal("MysteryEvent", unknown.EventType);
            Assert.Equal(99, unknown.PlayerId);

            var replayable = inspection.Entries[2];
            Assert.Equal(2, replayable.Index);
            Assert.Equal(EDeadLetterReason.Replayable, replayable.Reason);
            Assert.Equal(nameof(ItemUnlockedEvent), replayable.EventType);
            Assert.Equal(42, replayable.PlayerId);
        }

        [Fact]
        public async Task InspectAsync_RespectsMax_WhileStillReportingFullDepth()
        {
            using var scope = CreateScope();
            await SeedDeadLettersAsync(scope,
                EnvelopeFor(new ItemUnlockedEvent(1, 1)),
                EnvelopeFor(new ItemUnlockedEvent(2, 2)),
                EnvelopeFor(new ItemUnlockedEvent(3, 3)));

            var deadLetters = scope.ServiceProvider.GetRequiredService<IPlayerUpdateDeadLetters>();
            var inspection = await deadLetters.InspectAsync(2);

            // The page is capped at the requested max, but the reported depth is the full queue.
            Assert.Equal(3, inspection.TotalCount);
            Assert.Equal(2, inspection.Entries.Count);
            Assert.Equal([1, 2], inspection.Entries.Select(e => e.PlayerId));
        }

        [Fact]
        public async Task ReplayAllAsync_MovesEveryEntryOntoThePlayerQueue_AndEmptiesTheDeadLetterQueue()
        {
            using var scope = CreateScope();
            var first = EnvelopeFor(new ItemUnlockedEvent(1, 10));
            var second = EnvelopeFor(new ItemUnlockedEvent(2, 20));
            await SeedDeadLettersAsync(scope, first, second);

            var deadLetters = scope.ServiceProvider.GetRequiredService<IPlayerUpdateDeadLetters>();
            var result = await deadLetters.ReplayAllAsync();

            Assert.Equal(2, result.ReplayedCount);
            Assert.Equal(0, result.RemainingCount);

            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            Assert.Equal(0, await pubsub.GetQueue(Constants.PUBSUB_PLAYER_DEAD_LETTER_QUEUE).GetLengthAsync());
            // The entries land on the player update queue in their original order, ready to be re-drained.
            Assert.Equal([first, second], await pubsub.GetQueue(Constants.PUBSUB_PLAYER_QUEUE).PeekAsync(10));
        }

        [Fact]
        public async Task ReplayAllAsync_SkipsMalformedAndUnknownTypeEntries_LeavingThemQueued()
        {
            using var scope = CreateScope();
            var malformed = "this-is-not-json";
            var unknown = Envelope("MysteryEvent", "{\"playerId\":99}");
            var replayable = EnvelopeFor(new ItemUnlockedEvent(PlayerId: 1, ItemId: 10));
            await SeedDeadLettersAsync(scope, malformed, unknown, replayable);

            var deadLetters = scope.ServiceProvider.GetRequiredService<IPlayerUpdateDeadLetters>();
            var result = await deadLetters.ReplayAllAsync();

            // Only the genuinely replayable entry moves; a malformed or unknown-type entry would just
            // bounce straight back onto this same queue, so it stays put (matching the sibling
            // socket-command DLQ contract: a non-replayable entry stays queued through both peek and replay).
            Assert.Equal(1, result.ReplayedCount);
            Assert.Equal(2, result.RemainingCount);

            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            Assert.Equal([malformed, unknown], await pubsub.GetQueue(Constants.PUBSUB_PLAYER_DEAD_LETTER_QUEUE).PeekAsync(10));
            Assert.Equal([replayable], await pubsub.GetQueue(Constants.PUBSUB_PLAYER_QUEUE).PeekAsync(10));
        }

        [Fact]
        public async Task ReplaySelectedAsync_StillReplaysAnExplicitlySelectedPoisonEntry()
        {
            using var scope = CreateScope();
            var unknown = Envelope("MysteryEvent", "{\"playerId\":99}");
            await SeedDeadLettersAsync(scope, unknown);

            var deadLetters = scope.ServiceProvider.GetRequiredService<IPlayerUpdateDeadLetters>();
            // Unlike ReplayAllAsync, a caller-selected replay honours the operator's explicit choice even
            // for a non-replayable entry (the admin UI surfaces a warning for this rather than blocking it).
            var result = await deadLetters.ReplaySelectedAsync([unknown]);

            Assert.Equal(1, result.ReplayedCount);
            Assert.Equal(0, result.RemainingCount);

            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            Assert.Equal([unknown], await pubsub.GetQueue(Constants.PUBSUB_PLAYER_QUEUE).PeekAsync(10));
        }

        [Fact]
        public async Task ReplayAllAsync_MovesDuplicatePayloads_RespectingMultiplicity()
        {
            using var scope = CreateScope();
            var evt = EnvelopeFor(new ItemUnlockedEvent(PlayerId: 1, ItemId: 10));
            await SeedDeadLettersAsync(scope, evt, evt);

            var deadLetters = scope.ServiceProvider.GetRequiredService<IPlayerUpdateDeadLetters>();
            var result = await deadLetters.ReplayAllAsync();

            Assert.Equal(2, result.ReplayedCount);
            Assert.Equal(0, result.RemainingCount);

            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            Assert.Equal([evt, evt], await pubsub.GetQueue(Constants.PUBSUB_PLAYER_QUEUE).PeekAsync(10));
        }

        [Fact]
        public async Task ReplayAllAsync_AllEntriesPoison_ReplaysNothing_AndLeavesTheQueueUntouched()
        {
            using var scope = CreateScope();
            var malformed = "this-is-not-json";
            await SeedDeadLettersAsync(scope, malformed);

            var deadLetters = scope.ServiceProvider.GetRequiredService<IPlayerUpdateDeadLetters>();
            var result = await deadLetters.ReplayAllAsync();

            Assert.Equal(0, result.ReplayedCount);
            Assert.Equal(1, result.RemainingCount);

            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            Assert.Equal(0, await pubsub.GetQueue(Constants.PUBSUB_PLAYER_QUEUE).GetLengthAsync());
            Assert.Equal([malformed], await pubsub.GetQueue(Constants.PUBSUB_PLAYER_DEAD_LETTER_QUEUE).PeekAsync(10));
        }

        [Fact]
        public async Task ReplaySelectedAsync_MovesOnlyTheChosenEntries_LeavingTheRest()
        {
            using var scope = CreateScope();
            var keep = EnvelopeFor(new ItemUnlockedEvent(1, 10));
            var replay = EnvelopeFor(new ItemUnlockedEvent(2, 20));
            await SeedDeadLettersAsync(scope, keep, replay);

            var deadLetters = scope.ServiceProvider.GetRequiredService<IPlayerUpdateDeadLetters>();
            var result = await deadLetters.ReplaySelectedAsync([replay]);

            Assert.Equal(1, result.ReplayedCount);
            Assert.Equal(1, result.RemainingCount);

            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            // Only the chosen entry moved; the unselected one stays on the dead-letter queue.
            Assert.Equal([keep], await pubsub.GetQueue(Constants.PUBSUB_PLAYER_DEAD_LETTER_QUEUE).PeekAsync(10));
            Assert.Equal([replay], await pubsub.GetQueue(Constants.PUBSUB_PLAYER_QUEUE).PeekAsync(10));
        }

        [Fact]
        public async Task ReplaySelectedAsync_IgnoresPayloadsNotOnTheQueue_NeverInjectingThemOntoThePlayerQueue()
        {
            using var scope = CreateScope();
            var real = EnvelopeFor(new ItemUnlockedEvent(1, 10));
            await SeedDeadLettersAsync(scope, real);

            var deadLetters = scope.ServiceProvider.GetRequiredService<IPlayerUpdateDeadLetters>();
            // A fabricated payload that was never dead-lettered must be skipped, so replay can't be used to
            // push an arbitrary message onto the player update queue.
            var result = await deadLetters.ReplaySelectedAsync(["{\"type\":\"Injected\",\"payload\":\"{}\"}"]);

            Assert.Equal(0, result.ReplayedCount);
            Assert.Equal(1, result.RemainingCount);

            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            Assert.Equal(0, await pubsub.GetQueue(Constants.PUBSUB_PLAYER_QUEUE).GetLengthAsync());
            Assert.Equal([real], await pubsub.GetQueue(Constants.PUBSUB_PLAYER_DEAD_LETTER_QUEUE).PeekAsync(10));
        }

        private static async Task SeedDeadLettersAsync(IServiceScope scope, params string[] messages)
        {
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            await pubsub.GetQueue(Constants.PUBSUB_PLAYER_DEAD_LETTER_QUEUE).AddRangeToQueueAsync(messages);
        }

        private static string Envelope(string type, string payloadJson)
            => new DomainEventEnvelope { Type = type, Payload = payloadJson }.Serialize();

        private static string EnvelopeFor<T>(T evt) where T : IDomainEvent
            => new DomainEventEnvelope { Type = typeof(T).Name, Payload = evt.Serialize() }.Serialize();
    }
}
