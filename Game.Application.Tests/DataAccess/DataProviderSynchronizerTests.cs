using Game.Abstractions.Infrastructure;
using Game.Core;
using Game.Core.Events;
using Game.Core.Players.Events;
using Game.Core.Progress;
using Game.DataAccess;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Verifies that <see cref="DataProviderSynchronizer"/> no longer silently swallows or drops events while draining
    /// the player update queue: malformed payloads are dead-lettered, an unexpected failure is retried with backoff and
    /// dead-lettered only once the retries are exhausted, a transient failure that later succeeds is persisted, and in
    /// every case the failing message does not stop the remaining queued events from being processed.
    /// </summary>
    [Collection("Integration")]
    public class DataProviderSynchronizerTests : ApplicationIntegrationTestBase
    {
        // A zero-delay policy keeps the retry loop fast in tests while still exercising the full attempt count.
        private static readonly PlayerUpdateRetryPolicy TestRetryPolicy = new(maxAttempts: 3, baseDelay: TimeSpan.Zero);

        public DataProviderSynchronizerTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        [Fact]
        public async Task ProcessQueue_MalformedEventBeforeValidEvent_DeadLettersItAndStillPersistsValidEvent()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, level: 5, zoneId: 0);

            var validEvent = new PlayerCoreUpdatedEvent(
                PlayerId: player.Id,
                Level: 9,
                Exp: 1234,
                CurrentZoneId: 0,
                StatPointsGained: 100,
                StatPointsUsed: 100,
                LastActivity: DateTime.UtcNow,
                AutoChallengeBoss: false);

            var logger = new CapturingLogger<DataProviderSynchronizer>();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var synchronizer = new DataProviderSynchronizer(scope.ServiceProvider, pubsub, logger, TestRetryPolicy);

            // The malformed message is dequeued first; the synchronizer must dead-letter it and still apply the valid one.
            var queue = new InMemoryPubSubQueue("this is not a valid envelope", Serialize(validEvent));

            await synchronizer.ProcessQueue(queue);

            // The malformed payload is surfaced as a warning rather than silently swallowed.
            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);

            // The malformed message lands on the dead-letter queue rather than being dropped.
            var deadLettered = await DrainDeadLetterQueue(pubsub);
            Assert.Equal(["this is not a valid envelope"], deadLettered);

            // The whole queue was drained, and the valid event after the malformed one was still persisted.
            Assert.Null(await queue.GetNextAsync());

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var persisted = await verifyContext.Players.FindAsync([player.Id], CancellationToken);
            Assert.NotNull(persisted);
            Assert.Equal(9, persisted.Level);
            Assert.Equal(1234, persisted.Exp);
        }

        [Fact]
        public async Task ProcessQueue_UnexpectedFailureDuringHandling_RetriesThenDeadLettersAndContinues()
        {
            // An empty provider has no GameContext registered, so HandleEvent throws InvalidOperationException —
            // standing in for an unexpected failure (e.g. a database error) that persists across every retry.
            var brokenServices = new ServiceCollection().BuildServiceProvider();

            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var logger = new CapturingLogger<DataProviderSynchronizer>();
            var synchronizer = new DataProviderSynchronizer(brokenServices, pubsub, logger, TestRetryPolicy);

            var firstEvent = new PlayerCoreUpdatedEvent(1, 2, 3, 0, 100, 100, DateTime.UtcNow, false);
            var secondEvent = new PlayerCoreUpdatedEvent(2, 3, 4, 0, 100, 100, DateTime.UtcNow, false);
            var queue = new InMemoryPubSubQueue(Serialize(firstEvent), Serialize(secondEvent));

            await synchronizer.ProcessQueue(queue);

            // Each event is attempted MaxAttempts times: the non-final attempts log a "retrying" warning and the
            // final attempt logs an error before dead-lettering, and the first failure did not stop the second.
            Assert.Equal((TestRetryPolicy.MaxAttempts - 1) * 2, logger.Entries.Count(e => e.Level == LogLevel.Warning && e.Message.Contains("retrying")));
            Assert.Equal(2, logger.Entries.Count(e => e.Level == LogLevel.Error));
            // The now-growing dead-letter queue is surfaced once the drain settles so the backlog isn't invisible.
            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("dead-letter queue"));
            Assert.Null(await queue.GetNextAsync());

            // Both events that exhausted their retries are preserved on the dead-letter queue rather than dropped.
            var deadLettered = await DrainDeadLetterQueue(pubsub);
            Assert.Equal(2, deadLettered.Count);
        }

        [Fact]
        public async Task ProcessQueue_TransientFailureThatLaterSucceeds_RetriesAndPersistsWithoutDeadLettering()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, level: 5, zoneId: 0);

            var validEvent = new PlayerCoreUpdatedEvent(
                PlayerId: player.Id,
                Level: 12,
                Exp: 4321,
                CurrentZoneId: 0,
                StatPointsGained: 100,
                StatPointsUsed: 100,
                LastActivity: DateTime.UtcNow,
                AutoChallengeBoss: false);

            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var logger = new CapturingLogger<DataProviderSynchronizer>();

            // The first scope creation throws (a simulated transient failure); the retry creates a real scope and persists.
            var flakyServices = new FlakyServiceProvider(scope.ServiceProvider, failuresBeforeSuccess: 1);
            var synchronizer = new DataProviderSynchronizer(flakyServices, pubsub, logger, TestRetryPolicy);

            var queue = new InMemoryPubSubQueue(Serialize(validEvent));

            await synchronizer.ProcessQueue(queue);

            // The transient failure was retried (one warning) and ultimately succeeded (no error, nothing dead-lettered).
            Assert.Equal(2, flakyServices.ScopeCreations);
            Assert.Equal(1, logger.Entries.Count(e => e.Level == LogLevel.Warning));
            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
            Assert.Empty(await DrainDeadLetterQueue(pubsub));

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var persisted = await verifyContext.Players.FindAsync([player.Id], CancellationToken);
            Assert.NotNull(persisted);
            Assert.Equal(12, persisted.Level);
            Assert.Equal(4321, persisted.Exp);
        }

        [Fact]
        public async Task ProcessQueue_WellFormedEnvelopeWithMalformedInnerPayload_DeadLettersWithoutRetrying()
        {
            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var logger = new CapturingLogger<DataProviderSynchronizer>();
            var synchronizer = new DataProviderSynchronizer(scope.ServiceProvider, pubsub, logger, TestRetryPolicy);

            // The envelope parses cleanly (known event type), but its inner payload is not valid JSON, so
            // HandleEvent throws a JsonException — a poison message that must be dead-lettered without retrying.
            var envelope = new DomainEventEnvelope
            {
                Type = nameof(PlayerCoreUpdatedEvent),
                Payload = "this is not valid json",
            };
            var message = envelope.Serialize();
            var queue = new InMemoryPubSubQueue(message);

            await synchronizer.ProcessQueue(queue);

            // A poison inner payload is surfaced as a warning (not retried, so no error) and dead-lettered verbatim.
            Assert.Equal(1, logger.Entries.Count(e => e.Level == LogLevel.Warning && e.Message.Contains("Dead-lettering")));
            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
            Assert.Equal([message], await DrainDeadLetterQueue(pubsub));
        }

        [Fact]
        public async Task ProcessQueue_UnknownEventType_DeadLettersWithoutRetrying()
        {
            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var logger = new CapturingLogger<DataProviderSynchronizer>();
            var synchronizer = new DataProviderSynchronizer(scope.ServiceProvider, pubsub, logger, TestRetryPolicy);

            // A well-formed envelope whose Type matches no registered handler case — e.g. a new event
            // added to PlayerPersistencePublisher without a corresponding case in HandleEvent.
            var envelope = new DomainEventEnvelope
            {
                Type = "UnregisteredEventType",
                Payload = "{}",
            };
            var message = envelope.Serialize();
            var queue = new InMemoryPubSubQueue(message);

            await synchronizer.ProcessQueue(queue);

            // An unknown type is a poison message — no retry can fix it — so it is dead-lettered
            // immediately with a warning and without escalating to an error.
            Assert.Equal(1, logger.Entries.Count(e => e.Level == LogLevel.Warning && e.Message.Contains("Dead-lettering")));
            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
            Assert.Equal([message], await DrainDeadLetterQueue(pubsub));
        }

        [Fact]
        public async Task ProcessQueue_SkillUnlockedEvent_InsertsUnselectedPlayerSkillIdempotently()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, level: 5, zoneId: 0);
            var skill = await TestDataSeeder.CreateSkillAsync(context);

            var evt = new SkillUnlockedEvent(player.Id, skill.Id);

            var logger = new CapturingLogger<DataProviderSynchronizer>();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var synchronizer = new DataProviderSynchronizer(scope.ServiceProvider, pubsub, logger, TestRetryPolicy);

            // The same unlock is delivered twice (e.g. a retry, or two challenges granting the same skill);
            // the idempotent insert must leave exactly one row, unselected and at order 0.
            var queue = new InMemoryPubSubQueue(Serialize(evt), Serialize(evt));

            await synchronizer.ProcessQueue(queue);

            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
            Assert.Empty(await DrainDeadLetterQueue(pubsub));

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var rows = await verifyContext.PlayerSkills
                .Where(ps => ps.PlayerId == player.Id && ps.SkillId == skill.Id)
                .ToListAsync(CancellationToken);
            var row = Assert.Single(rows);
            Assert.False(row.Selected);
            Assert.Equal(0, row.Order);
        }

        [Fact]
        public async Task ProcessQueue_SelectedSkillsChangedEvent_ReplacesEquippedSetAndOrderIdempotently()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, level: 5, zoneId: 0);
            var skill0 = await TestDataSeeder.CreateSkillAsync(context, name: "S0");
            var skill1 = await TestDataSeeder.CreateSkillAsync(context, name: "S1");
            var skill2 = await TestDataSeeder.CreateSkillAsync(context, name: "S2");

            // Starting loadout: skill0 (order 0), skill1 (order 1) equipped; skill2 unlocked but not equipped.
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill0.Id, selected: true, order: 0);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill1.Id, selected: true, order: 1);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill2.Id, selected: false, order: 0);

            // New loadout swaps skill1 out for skill2 and reverses the survivor's position.
            var evt = new SelectedSkillsChangedEvent(player.Id, [skill2.Id, skill0.Id]);

            var logger = new CapturingLogger<DataProviderSynchronizer>();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var synchronizer = new DataProviderSynchronizer(scope.ServiceProvider, pubsub, logger, TestRetryPolicy);

            // Deliver the same event twice: the delete-then-rebuild handler must converge to one result.
            var queue = new InMemoryPubSubQueue(Serialize(evt), Serialize(evt));

            await synchronizer.ProcessQueue(queue);

            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
            Assert.Empty(await DrainDeadLetterQueue(pubsub));

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var rows = await verifyContext.PlayerSkills
                .Where(ps => ps.PlayerId == player.Id)
                .ToListAsync(CancellationToken);

            Assert.Equal(3, rows.Count);
            // skill2 is now first, skill0 second; skill1 is deselected and its order reset to 0.
            AssertSkillState(rows, skill2.Id, selected: true, order: 0);
            AssertSkillState(rows, skill0.Id, selected: true, order: 1);
            AssertSkillState(rows, skill1.Id, selected: false, order: 0);
        }

        [Fact]
        public async Task ProcessQueue_SelectedSkillsChangedEvent_ReorderOnly_UpdatesOrder()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, level: 5, zoneId: 0);
            var skill0 = await TestDataSeeder.CreateSkillAsync(context, name: "S0");
            var skill1 = await TestDataSeeder.CreateSkillAsync(context, name: "S1");

            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill0.Id, selected: true, order: 0);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill1.Id, selected: true, order: 1);

            // Same set, swapped positions.
            var evt = new SelectedSkillsChangedEvent(player.Id, [skill1.Id, skill0.Id]);

            var logger = new CapturingLogger<DataProviderSynchronizer>();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var synchronizer = new DataProviderSynchronizer(scope.ServiceProvider, pubsub, logger, TestRetryPolicy);

            await synchronizer.ProcessQueue(new InMemoryPubSubQueue(Serialize(evt)));

            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var rows = await verifyContext.PlayerSkills
                .Where(ps => ps.PlayerId == player.Id)
                .ToListAsync(CancellationToken);

            AssertSkillState(rows, skill1.Id, selected: true, order: 0);
            AssertSkillState(rows, skill0.Id, selected: true, order: 1);
        }

        [Fact]
        public async Task ProcessQueue_SelectedSkillsChangedEvent_DeselectToEmpty_ClearsAllSelections()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, level: 5, zoneId: 0);
            var skill0 = await TestDataSeeder.CreateSkillAsync(context, name: "S0");
            var skill1 = await TestDataSeeder.CreateSkillAsync(context, name: "S1");

            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill0.Id, selected: true, order: 0);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill1.Id, selected: true, order: 1);

            // An empty loadout deselects everything (the skills stay unlocked).
            var evt = new SelectedSkillsChangedEvent(player.Id, []);

            var logger = new CapturingLogger<DataProviderSynchronizer>();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var synchronizer = new DataProviderSynchronizer(scope.ServiceProvider, pubsub, logger, TestRetryPolicy);

            await synchronizer.ProcessQueue(new InMemoryPubSubQueue(Serialize(evt)));

            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var rows = await verifyContext.PlayerSkills
                .Where(ps => ps.PlayerId == player.Id)
                .ToListAsync(CancellationToken);

            // Both skills remain unlocked but are no longer equipped, with order reset to 0.
            Assert.Equal(2, rows.Count);
            AssertSkillState(rows, skill0.Id, selected: false, order: 0);
            AssertSkillState(rows, skill1.Id, selected: false, order: 0);
        }

        [Fact]
        public async Task ProcessQueue_ItemUnlockedEvent_InsertsUnlockedItemIdempotently()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, level: 5, zoneId: 0);
            var item = await TestDataSeeder.CreateItemAsync(context);

            var evt = new ItemUnlockedEvent(player.Id, item.Id);

            var logger = new CapturingLogger<DataProviderSynchronizer>();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var synchronizer = new DataProviderSynchronizer(scope.ServiceProvider, pubsub, logger, TestRetryPolicy);

            // The same unlock is delivered twice (e.g. a retry, or two challenges granting the same item);
            // the idempotent insert must leave exactly one row, unequipped and not favorited.
            var queue = new InMemoryPubSubQueue(Serialize(evt), Serialize(evt));

            await synchronizer.ProcessQueue(queue);

            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
            Assert.Empty(await DrainDeadLetterQueue(pubsub));

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var rows = await verifyContext.UnlockedItems
                .Where(ui => ui.PlayerId == player.Id && ui.ItemId == item.Id)
                .ToListAsync(CancellationToken);
            var row = Assert.Single(rows);
            Assert.Null(row.EquipmentSlotId);
            Assert.False(row.Favorite);
        }

        [Fact]
        public async Task ProcessQueue_ItemEquippedEvent_ClearsPriorOccupantAndEquipsIdempotently()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, level: 5, zoneId: 0);
            var occupant = await TestDataSeeder.CreateItemAsync(context, name: "Occupant");
            var incoming = await TestDataSeeder.CreateItemAsync(context, name: "Incoming");

            // The slot already holds one item; the incoming item is unlocked but unequipped.
            await TestDataSeeder.LinkItemToPlayerAsync(context, player.Id, occupant.Id, equipmentSlot: EEquipmentSlot.HelmSlot);
            await TestDataSeeder.LinkItemToPlayerAsync(context, player.Id, incoming.Id, equipmentSlot: null);

            var evt = new ItemEquippedEvent(player.Id, incoming.Id, (int)EEquipmentSlot.HelmSlot);

            var logger = new CapturingLogger<DataProviderSynchronizer>();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var synchronizer = new DataProviderSynchronizer(scope.ServiceProvider, pubsub, logger, TestRetryPolicy);

            // Delivered twice: the single-statement absolute upsert must converge — the incoming item holds the
            // slot, the prior occupant is cleared, and re-applying changes nothing.
            var queue = new InMemoryPubSubQueue(Serialize(evt), Serialize(evt));

            await synchronizer.ProcessQueue(queue);

            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
            Assert.Empty(await DrainDeadLetterQueue(pubsub));

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var rows = await verifyContext.UnlockedItems
                .Where(ui => ui.PlayerId == player.Id)
                .ToListAsync(CancellationToken);

            Assert.Equal((int)EEquipmentSlot.HelmSlot, Assert.Single(rows, ui => ui.ItemId == incoming.Id).EquipmentSlotId);
            Assert.Null(Assert.Single(rows, ui => ui.ItemId == occupant.Id).EquipmentSlotId);
        }

        [Fact]
        public async Task ProcessQueue_ItemEquippedEvent_MovingItemBetweenSlots_VacatesOldSlot()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, level: 5, zoneId: 0);
            var item = await TestDataSeeder.CreateItemAsync(context);

            // The item starts equipped in the Helm slot; the event re-equips it into the Chest slot.
            await TestDataSeeder.LinkItemToPlayerAsync(context, player.Id, item.Id, equipmentSlot: EEquipmentSlot.HelmSlot);

            var evt = new ItemEquippedEvent(player.Id, item.Id, (int)EEquipmentSlot.ChestSlot);

            var logger = new CapturingLogger<DataProviderSynchronizer>();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var synchronizer = new DataProviderSynchronizer(scope.ServiceProvider, pubsub, logger, TestRetryPolicy);

            await synchronizer.ProcessQueue(new InMemoryPubSubQueue(Serialize(evt)));

            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var row = await verifyContext.UnlockedItems
                .SingleAsync(ui => ui.PlayerId == player.Id && ui.ItemId == item.Id, CancellationToken);

            // The item moved to the new slot; reassigning its own row vacates the old slot in the same statement.
            Assert.Equal((int)EEquipmentSlot.ChestSlot, row.EquipmentSlotId);
        }

        [Fact]
        public async Task ProcessQueue_ModUnlockedEvent_InsertsUnlockedModIdempotently()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, level: 5, zoneId: 0);
            var mod = await TestDataSeeder.CreateItemModAsync(context);

            var evt = new ModUnlockedEvent(player.Id, mod.Id);

            var logger = new CapturingLogger<DataProviderSynchronizer>();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var synchronizer = new DataProviderSynchronizer(scope.ServiceProvider, pubsub, logger, TestRetryPolicy);

            // Delivered twice; the idempotent insert must leave exactly one row.
            var queue = new InMemoryPubSubQueue(Serialize(evt), Serialize(evt));

            await synchronizer.ProcessQueue(queue);

            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
            Assert.Empty(await DrainDeadLetterQueue(pubsub));

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var rows = await verifyContext.UnlockedMods
                .Where(um => um.PlayerId == player.Id && um.ItemModId == mod.Id)
                .ToListAsync(CancellationToken);
            Assert.Single(rows);
        }

        [Fact]
        public async Task ProcessQueue_ModAppliedEvent_AppliesModToSlotIdempotently()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, level: 5, zoneId: 0);

            // An item with one Prefix mod slot, and a mod to apply into it.
            var item = await TestDataSeeder.CreateItemAsync(context);
            var modSlot = new Infrastructure.Entities.ItemModSlot
            {
                ItemId = item.Id,
                ItemModSlotTypeId = (int)EItemModType.Prefix,
            };
            context.ItemModSlots.Add(modSlot);
            await context.SaveChangesAsync(CancellationToken);
            var mod = await TestDataSeeder.CreateItemModAsync(context);

            var evt = new ModAppliedEvent(player.Id, item.Id, modSlot.Id, mod.Id);

            var logger = new CapturingLogger<DataProviderSynchronizer>();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var synchronizer = new DataProviderSynchronizer(scope.ServiceProvider, pubsub, logger, TestRetryPolicy);

            // Delivered twice: the delete-then-insert handler must converge to a single applied mod in the slot.
            var queue = new InMemoryPubSubQueue(Serialize(evt), Serialize(evt));

            await synchronizer.ProcessQueue(queue);

            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
            Assert.Empty(await DrainDeadLetterQueue(pubsub));

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var rows = await verifyContext.AppliedMods
                .Where(am => am.PlayerId == player.Id && am.ItemId == item.Id && am.ItemModSlotId == modSlot.Id)
                .ToListAsync(CancellationToken);
            var row = Assert.Single(rows);
            Assert.Equal(mod.Id, row.ItemModId);
        }

        [Fact]
        public async Task ProcessQueue_ModAppliedEvent_ReplacesExistingModInSameSlot()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, level: 5, zoneId: 0);

            var item = await TestDataSeeder.CreateItemAsync(context);
            var modSlot = new Infrastructure.Entities.ItemModSlot
            {
                ItemId = item.Id,
                ItemModSlotTypeId = (int)EItemModType.Prefix,
            };
            context.ItemModSlots.Add(modSlot);
            await context.SaveChangesAsync(CancellationToken);
            var modA = await TestDataSeeder.CreateItemModAsync(context, name: "Mod A");
            var modB = await TestDataSeeder.CreateItemModAsync(context, name: "Mod B");

            // Applying a second mod to an already-filled slot must replace the first, not stack.
            var applyA = new ModAppliedEvent(player.Id, item.Id, modSlot.Id, modA.Id);
            var applyB = new ModAppliedEvent(player.Id, item.Id, modSlot.Id, modB.Id);

            var logger = new CapturingLogger<DataProviderSynchronizer>();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var synchronizer = new DataProviderSynchronizer(scope.ServiceProvider, pubsub, logger, TestRetryPolicy);

            var queue = new InMemoryPubSubQueue(Serialize(applyA), Serialize(applyB));

            await synchronizer.ProcessQueue(queue);

            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var rows = await verifyContext.AppliedMods
                .Where(am => am.PlayerId == player.Id && am.ItemId == item.Id && am.ItemModSlotId == modSlot.Id)
                .ToListAsync(CancellationToken);
            var row = Assert.Single(rows);
            Assert.Equal(modB.Id, row.ItemModId);
        }

        [Fact]
        public async Task ProcessQueue_AttributeAllocationsChangedEvent_UpsertsInsertsAndDeletesIdempotently()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            // The seeder gives the player Strength = 50 and Endurance = 50 allocations to start.
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, level: 5, zoneId: 0);

            // One event exercising all three branches: update an existing allocation (Strength),
            // delete an existing one by zeroing it (Endurance), and insert a brand-new one (Agility).
            var evt = new AttributeAllocationsChangedEvent(player.Id, new List<AttributeAllocationEntry>
            {
                new(EAttribute.Strength, 75d),
                new(EAttribute.Endurance, 0d),
                new(EAttribute.Agility, 30d),
            });

            var logger = new CapturingLogger<DataProviderSynchronizer>();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var synchronizer = new DataProviderSynchronizer(scope.ServiceProvider, pubsub, logger, TestRetryPolicy);

            // Delivered twice: re-applying the same allocations must converge to the same rows.
            var queue = new InMemoryPubSubQueue(Serialize(evt), Serialize(evt));

            await synchronizer.ProcessQueue(queue);

            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
            Assert.Empty(await DrainDeadLetterQueue(pubsub));

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var rows = await verifyContext.PlayerAttributes
                .Where(pa => pa.PlayerId == player.Id)
                .ToListAsync(CancellationToken);

            // Strength updated, Agility inserted, Endurance deleted (zeroed out).
            Assert.Equal(2, rows.Count);
            Assert.Equal(75m, Assert.Single(rows, pa => pa.AttributeId == (int)EAttribute.Strength).Amount);
            Assert.Equal(30m, Assert.Single(rows, pa => pa.AttributeId == (int)EAttribute.Agility).Amount);
            Assert.DoesNotContain(rows, pa => pa.AttributeId == (int)EAttribute.Endurance);
        }

        [Fact]
        public async Task ProcessQueue_LogPreferenceChangedEvent_UpdatesExistingPreferenceIdempotently()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, level: 5, zoneId: 0);

            // The player already has an enabled Damage-log preference; the event toggles it off.
            await TestDataSeeder.AddLogPreferenceAsync(context, player.Id, ELogType.Damage, enabled: true);

            var evt = new LogPreferenceChangedEvent(player.Id, ELogType.Damage, Enabled: false);

            var logger = new CapturingLogger<DataProviderSynchronizer>();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var synchronizer = new DataProviderSynchronizer(scope.ServiceProvider, pubsub, logger, TestRetryPolicy);

            // Delivered twice: the absolute update must converge to a single row toggled off.
            var queue = new InMemoryPubSubQueue(Serialize(evt), Serialize(evt));

            await synchronizer.ProcessQueue(queue);

            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
            Assert.Empty(await DrainDeadLetterQueue(pubsub));

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var rows = await verifyContext.LogPreferences
                .Where(lp => lp.PlayerId == player.Id && lp.LogTypeId == (int)ELogType.Damage)
                .ToListAsync(CancellationToken);
            var row = Assert.Single(rows);
            Assert.False(row.Enabled);
        }

        [Fact]
        public async Task ProcessQueue_LogPreferenceChangedEvent_FirstTimeInsertsPreferenceIdempotently()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, level: 5, zoneId: 0);

            // No preference row exists yet for this log type: the first apply must insert it, the second update it.
            var evt = new LogPreferenceChangedEvent(player.Id, ELogType.Exp, Enabled: false);

            var logger = new CapturingLogger<DataProviderSynchronizer>();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var synchronizer = new DataProviderSynchronizer(scope.ServiceProvider, pubsub, logger, TestRetryPolicy);

            // Delivered twice: the update-then-insert path must leave exactly one row (no duplicate-key failure).
            var queue = new InMemoryPubSubQueue(Serialize(evt), Serialize(evt));

            await synchronizer.ProcessQueue(queue);

            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
            Assert.Empty(await DrainDeadLetterQueue(pubsub));

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var rows = await verifyContext.LogPreferences
                .Where(lp => lp.PlayerId == player.Id && lp.LogTypeId == (int)ELogType.Exp)
                .ToListAsync(CancellationToken);
            var row = Assert.Single(rows);
            Assert.False(row.Enabled);
        }

        [Fact]
        public async Task StopAsync_NoDrainInFlight_CompletesWithoutWarning()
        {
            using var scope = CreateScope();
            var logger = new CapturingLogger<DataProviderSynchronizer>();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var synchronizer = new DataProviderSynchronizer(scope.ServiceProvider, pubsub, logger, TestRetryPolicy);

            // With no drain holding the gate, shutdown acquires it immediately and completes without the
            // bounded-wait warning.
            await synchronizer.StopAsync(CancellationToken.None);

            Assert.Empty(logger.Entries);
        }

        [Fact]
        public async Task StartAsync_CancellationAlreadyRequested_SkipsReclaimAndStartupDrain()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, level: 5, zoneId: 0);

            var validEvent = new PlayerCoreUpdatedEvent(
                PlayerId: player.Id,
                Level: 42,
                Exp: 4242,
                CurrentZoneId: 0,
                StatPointsGained: 100,
                StatPointsUsed: 100,
                LastActivity: DateTime.UtcNow,
                AutoChallengeBoss: false);

            var realPubSub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var queue = realPubSub.GetQueue(Constants.PUBSUB_PLAYER_QUEUE);
            await queue.AddToQueueAsync(Serialize(validEvent));

            var logger = new CapturingLogger<DataProviderSynchronizer>();
            var pubsub = new SubscribeSuppressingPubSubService(realPubSub);
            var synchronizer = new DataProviderSynchronizer(scope.ServiceProvider, pubsub, logger, TestRetryPolicy);

            // A host that cancels startup mid-boot must not run the reclaim/drain: the token is honored, so the
            // item is left on the queue (to be drained on a later, uncancelled startup) rather than applied.
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();
            await synchronizer.StartAsync(cts.Token);

            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);

            // The event was never drained: it is still waiting on the queue and the player is unchanged.
            Assert.Equal(Serialize(validEvent), await queue.GetNextAsync());

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var persisted = await verifyContext.Players.FindAsync([player.Id], CancellationToken);
            Assert.NotNull(persisted);
            Assert.Equal(5, persisted.Level);
        }

        [Fact]
        public async Task StopAsync_DrainInFlight_StopsAtCleanBoundaryAndAwaitsIt()
        {
            using var scope = CreateScope();
            var logger = new CapturingLogger<DataProviderSynchronizer>();

            // Two malformed items so processing needs no database: each is dead-lettered, and the gate sits on
            // the acknowledge of the first so the drain is provably in flight (holding the drain gate) when the
            // stop arrives.
            var gatedQueue = new GatedDrainQueue("malformed-1", "malformed-2");
            var pubsub = new SingleQueuePubSubService(gatedQueue);
            var synchronizer = new DataProviderSynchronizer(scope.ServiceProvider, pubsub, logger, TestRetryPolicy, drainTimeout: TimeSpan.FromSeconds(5));

            // The startup drain reserves + dead-letters the first item, then blocks inside its acknowledge.
            var startTask = synchronizer.StartAsync(CancellationToken.None);
            await gatedQueue.AcknowledgeReached.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // Stop while that drain holds the gate. StopAsync must not complete until the gate is released, so it
            // is still pending while the drain is parked.
            var stopTask = synchronizer.StopAsync(CancellationToken.None);
            Assert.False(stopTask.IsCompleted);

            // Let the acknowledge finish. With stopping signalled, the drain loop exits at the boundary instead
            // of reserving the second item, releasing the gate so StopAsync can complete.
            gatedQueue.ReleaseAcknowledge.SetResult();
            await stopTask.WaitAsync(TimeSpan.FromSeconds(5));
            await startTask.WaitAsync(TimeSpan.FromSeconds(5));

            // The drain stopped at a clean boundary: only the first item was reserved; the second is untouched
            // and still waiting (reclaimed/drained on the next startup).
            Assert.Equal(1, gatedQueue.ReservedCount);
            Assert.Equal("malformed-2", await gatedQueue.GetNextAsync());

            // It completed within the bounded wait, so no give-up warning was logged.
            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("did not complete"));
            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
        }

        [Fact]
        public async Task StopAsync_WedgedReserveDuringDrain_UnwindsPromptlyViaThreadedToken()
        {
            using var scope = CreateScope();
            var logger = new CapturingLogger<DataProviderSynchronizer>();

            // The reserve wedges (a stand-in for a stuck Redis round-trip) and only unwinds if the synchronizer
            // threads a cancelable token into it — exactly the refinement this exercises. The drain timeout is
            // generous so a prompt unwind is provably the threaded token at work, not the bounded give-up firing.
            var wedgedQueue = new WedgedReserveQueue();
            var pubsub = new SingleQueuePubSubService(wedgedQueue);
            var synchronizer = new DataProviderSynchronizer(scope.ServiceProvider, pubsub, logger, TestRetryPolicy, drainTimeout: TimeSpan.FromSeconds(30));

            // The startup drain reserves once and then parks inside the wedged reserve, holding the drain gate.
            var startTask = synchronizer.StartAsync(CancellationToken.None);
            await wedgedQueue.ReserveReached.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(startTask.IsCompleted);

            // Stopping cancels the token threaded into the reserve, so the wedged round-trip unwinds at once rather
            // than blocking until the drain timeout; the OCE is treated as a clean stop, never surfaced as an error.
            var stopTask = synchronizer.StopAsync(CancellationToken.None);
            await Task.WhenAll(startTask.WaitAsync(TimeSpan.FromSeconds(5)), stopTask.WaitAsync(TimeSpan.FromSeconds(5)));

            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("did not complete"));
        }

        [Fact]
        public async Task ProcessQueue_StopDuringRetryBackoff_CancelsTheBackoffAndLeavesItemReclaimable()
        {
            // A long backoff makes a prompt unwind provably the threaded cancellation at work rather than the
            // delay simply elapsing.
            var slowBackoff = new PlayerUpdateRetryPolicy(maxAttempts: 3, baseDelay: TimeSpan.FromSeconds(30));

            // An empty provider has no GameContext, so HandleEvent throws on every attempt — the event enters
            // the retry backoff between attempts, which is the dead time this exercises.
            var brokenServices = new ServiceCollection().BuildServiceProvider();

            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var logger = new CapturingLogger<DataProviderSynchronizer>();
            var synchronizer = new DataProviderSynchronizer(brokenServices, pubsub, logger, slowBackoff);

            var queue = new InMemoryPubSubQueue(Serialize(new PlayerCoreUpdatedEvent(1, 2, 3, 0, 100, 100, DateTime.UtcNow, false)));

            using var cts = new CancellationTokenSource();
            var drainTask = synchronizer.ProcessQueue(queue, cts.Token);

            // Wait until the first attempt has failed and the drain is parked inside the 30s backoff.
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (!logger.Entries.Any(e => e.Level == LogLevel.Warning && e.Message.Contains("retrying")))
            {
                Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for the retry backoff to begin.");
                await Task.Delay(10, CancellationToken);
            }

            // Cancelling unwinds the backoff at once rather than waiting the 30s out; the OCE is a clean stop,
            // never surfaced as an error.
            await cts.CancelAsync();
            await drainTask.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);

            // The reserved item was never acknowledged, so it stays on the processing list to be reclaimed on
            // the next startup rather than being lost.
            Assert.Equal(1, await queue.ReclaimProcessingAsync());
        }

        [Fact]
        public async Task StartAsync_SubscribeThrows_PropagatesException()
        {
            using var scope = CreateScope();
            var logger = new CapturingLogger<DataProviderSynchronizer>();
            var throwingPubSub = new ThrowingPubSubService();
            var synchronizer = new DataProviderSynchronizer(scope.ServiceProvider, throwingPubSub, logger, TestRetryPolicy);

            await Assert.ThrowsAsync<InvalidOperationException>(() => synchronizer.StartAsync(CancellationToken.None));
        }

        [Fact]
        public async Task StartAsync_DrainsItemsAlreadyOnQueue_WithoutWaitingForAWake()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, level: 5, zoneId: 0);

            var validEvent = new PlayerCoreUpdatedEvent(
                PlayerId: player.Id,
                Level: 15,
                Exp: 5678,
                CurrentZoneId: 0,
                StatPointsGained: 100,
                StatPointsUsed: 100,
                LastActivity: DateTime.UtcNow,
                AutoChallengeBoss: false);

            var realPubSub = scope.ServiceProvider.GetRequiredService<IPubSubService>();

            // Enqueue an event before the synchronizer starts, with no wake published — modelling an item
            // stranded on the queue across an instance restart / dropped wake (#560). Only the startup drain
            // can pick it up, since nothing will publish a subsequent wake.
            await realPubSub.GetQueue(Constants.PUBSUB_PLAYER_QUEUE).AddToQueueAsync(Serialize(validEvent));

            var logger = new CapturingLogger<DataProviderSynchronizer>();
            // Subscribe is suppressed so the test exercises only the startup drain (and leaves no lingering
            // subscription on the shared channel that a later test's publish could trigger); GetQueue and the
            // dead-letter writes still route to real Redis.
            var pubsub = new SubscribeSuppressingPubSubService(realPubSub);
            var synchronizer = new DataProviderSynchronizer(scope.ServiceProvider, pubsub, logger, TestRetryPolicy);

            await synchronizer.StartAsync(CancellationToken.None);

            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
            Assert.Empty(await DrainDeadLetterQueue(realPubSub));

            // The startup drain consumed the whole queue.
            Assert.Null(await realPubSub.GetQueue(Constants.PUBSUB_PLAYER_QUEUE).GetNextAsync());

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var persisted = await verifyContext.Players.FindAsync([player.Id], CancellationToken);
            Assert.NotNull(persisted);
            Assert.Equal(15, persisted.Level);
            Assert.Equal(5678, persisted.Exp);
        }

        [Fact]
        public async Task StartAsync_ReclaimsInFlightItemOrphanedByCrashedRun_AndApplies()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, level: 5, zoneId: 0);

            var validEvent = new PlayerCoreUpdatedEvent(
                PlayerId: player.Id,
                Level: 21,
                Exp: 9876,
                CurrentZoneId: 0,
                StatPointsGained: 100,
                StatPointsUsed: 100,
                LastActivity: DateTime.UtcNow,
                AutoChallengeBoss: false);

            var realPubSub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var queue = realPubSub.GetQueue(Constants.PUBSUB_PLAYER_QUEUE);

            // Model a run that crashed mid-apply: it reserved the event (moving it off the main queue onto the
            // processing list) but died before acknowledging it, so the destructive-pop behaviour would have lost
            // it entirely. Reserve-without-acknowledge leaves it stranded on the processing list (#769).
            await queue.AddToQueueAsync(Serialize(validEvent));
            var reserved = await queue.ReserveNextAsync();
            Assert.NotNull(reserved);
            Assert.Null(await queue.GetNextAsync());

            var logger = new CapturingLogger<DataProviderSynchronizer>();
            // Subscribe is suppressed so the test exercises only the startup reclaim + drain (and leaves no
            // lingering subscription); GetQueue and the dead-letter writes still route to real Redis.
            var pubsub = new SubscribeSuppressingPubSubService(realPubSub);
            var synchronizer = new DataProviderSynchronizer(scope.ServiceProvider, pubsub, logger, TestRetryPolicy);

            // Startup reclaims the orphaned in-flight item back onto the queue and the startup drain applies it.
            await synchronizer.StartAsync(CancellationToken.None);

            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
            Assert.Empty(await DrainDeadLetterQueue(realPubSub));

            // The reclaimed event was applied and nothing is left waiting or in flight.
            Assert.Null(await queue.GetNextAsync());
            Assert.Equal(0, await queue.ReclaimProcessingAsync());

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var persisted = await verifyContext.Players.FindAsync([player.Id], CancellationToken);
            Assert.NotNull(persisted);
            Assert.Equal(21, persisted.Level);
            Assert.Equal(9876, persisted.Exp);
        }

        [Fact]
        public async Task ProcessQueue_ProgressUpdatedEvent_UpsertsStatisticsAndChallengesIdempotently()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            var challenge = await TestDataSeeder.CreateChallengeAsync(context);

            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var logger = new CapturingLogger<DataProviderSynchronizer>();
            var synchronizer = new DataProviderSynchronizer(scope.ServiceProvider, pubsub, logger, TestRetryPolicy);

            // First event inserts the rows. A global (null entity) stat and its per-entity twin exercise the
            // (StatisticTypeId, EntityId) keying and the entity-id-constrained load.
            await synchronizer.ProcessQueue(new InMemoryPubSubQueue(SerializeProgress(new ProgressUpdatedEvent
            {
                PlayerId = player.Id,
                Statistics =
                [
                    new CachedPlayerStatistic { StatisticTypeId = (int)EStatisticType.EnemiesKilled, EntityId = null, Value = 3m },
                    new CachedPlayerStatistic { StatisticTypeId = (int)EStatisticType.EnemiesKilled, EntityId = 7, Value = 1m },
                ],
                Challenges = [new CachedPlayerChallenge { ChallengeId = challenge.Id, Progress = 4m, Completed = false, CompletedAt = null }],
            })));

            using (var verify = CreateScope())
            {
                var ctx = verify.ServiceProvider.GetRequiredService<GameContext>();
                var stats = await ctx.PlayerStatistics.AsNoTracking().Where(s => s.PlayerId == player.Id).ToListAsync(CancellationToken);
                Assert.Equal(2, stats.Count);
                Assert.Equal(3m, stats.Single(s => s.EntityId == null).Value);
                Assert.Equal(1m, stats.Single(s => s.EntityId == 7).Value);
                var ch = await ctx.PlayerChallenges.AsNoTracking().SingleAsync(c => c.PlayerId == player.Id, CancellationToken);
                Assert.Equal(4m, ch.Progress);
                Assert.False(ch.Completed);
            }

            // Re-applying with new absolute values converges in place — each (type, entity) key updates its own
            // row with no duplicates and no cross-contamination between the global and per-entity rows.
            await synchronizer.ProcessQueue(new InMemoryPubSubQueue(SerializeProgress(new ProgressUpdatedEvent
            {
                PlayerId = player.Id,
                Statistics =
                [
                    new CachedPlayerStatistic { StatisticTypeId = (int)EStatisticType.EnemiesKilled, EntityId = null, Value = 10m },
                    new CachedPlayerStatistic { StatisticTypeId = (int)EStatisticType.EnemiesKilled, EntityId = 7, Value = 5m },
                ],
                Challenges = [new CachedPlayerChallenge { ChallengeId = challenge.Id, Progress = 10m, Completed = true, CompletedAt = DateTime.UtcNow }],
            })));

            using (var verify = CreateScope())
            {
                var ctx = verify.ServiceProvider.GetRequiredService<GameContext>();
                var stats = await ctx.PlayerStatistics.AsNoTracking().Where(s => s.PlayerId == player.Id).ToListAsync(CancellationToken);
                Assert.Equal(2, stats.Count);
                Assert.Equal(10m, stats.Single(s => s.EntityId == null).Value);
                Assert.Equal(5m, stats.Single(s => s.EntityId == 7).Value);
                var ch = await ctx.PlayerChallenges.AsNoTracking().SingleAsync(c => c.PlayerId == player.Id, CancellationToken);
                Assert.Equal(10m, ch.Progress);
                Assert.True(ch.Completed);
            }

            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
        }

        [Fact]
        public async Task ProcessQueue_ConcurrentDrains_SerializeAndApplyOrderSensitiveEventsInOrder()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, level: 5, zoneId: 0);
            var item = await TestDataSeeder.CreateItemAsync(context);
            await TestDataSeeder.LinkItemToPlayerAsync(context, player.Id, item.Id, equipmentSlot: null);

            // Order-sensitive pair for the same player: equip the item into a slot, then unequip it. Applied in
            // order the slot ends empty; applied out of order (unequip before equip) it ends wrongly occupied —
            // the corruption two concurrent drains popping the same queue could cause (#578).
            var equip = new ItemEquippedEvent(player.Id, item.Id, (int)EEquipmentSlot.HelmSlot);
            var unequip = new ItemUnequippedEvent(player.Id, item.Id);

            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var logger = new CapturingLogger<DataProviderSynchronizer>();
            var synchronizer = new DataProviderSynchronizer(scope.ServiceProvider, pubsub, logger, TestRetryPolicy);

            // The pop delay widens the window so two drains would overlap if the synchronizer let them run
            // concurrently — turning the otherwise-racy bug into a deterministic failure on the unfixed code.
            var queue = new ConcurrencyTrackingQueue(TimeSpan.FromMilliseconds(25), Serialize(equip), Serialize(unequip));

            // Two wakes fire at once, modelling the background worker dispatching a second drain mid-drain.
            await Task.WhenAll(synchronizer.ProcessQueue(queue), synchronizer.ProcessQueue(queue));

            // The drains were serialized: no two pops ever overlapped, so the atomic LPOPs could not interleave.
            Assert.Equal(1, queue.MaxObservedConcurrency);
            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
            Assert.Null(await queue.GetNextAsync());

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var row = await verifyContext.UnlockedItems
                .SingleAsync(ui => ui.PlayerId == player.Id && ui.ItemId == item.Id, CancellationToken);

            // Events applied in order, so the final state is the unequip: the slot is empty.
            Assert.Null(row.EquipmentSlotId);
        }

        private static void AssertSkillState(IEnumerable<Infrastructure.Entities.PlayerSkill> rows, int skillId, bool selected, int order)
        {
            var row = Assert.Single(rows, ps => ps.SkillId == skillId);
            Assert.Equal(selected, row.Selected);
            Assert.Equal(order, row.Order);
        }

        private static async Task<List<string>> DrainDeadLetterQueue(IPubSubService pubsub)
        {
            var deadLetterQueue = pubsub.GetQueue(Constants.PUBSUB_PLAYER_DEAD_LETTER_QUEUE);
            var drained = new List<string>();
            var next = await deadLetterQueue.GetNextAsync();
            while (next is not null)
            {
                drained.Add(next);
                next = await deadLetterQueue.GetNextAsync();
            }

            return drained;
        }

        private static string Serialize<T>(T evt) where T : IDomainEvent
        {
            var envelope = new DomainEventEnvelope
            {
                Type = typeof(T).Name,
                Payload = evt.Serialize(),
            };

            return envelope.Serialize();
        }

        // ProgressUpdatedEvent is a data-tier persistence payload (not an IDomainEvent), published by the
        // progress repo directly, so it gets its own envelope wrapper rather than the IDomainEvent helper.
        private static string SerializeProgress(ProgressUpdatedEvent evt)
        {
            var envelope = new DomainEventEnvelope
            {
                Type = nameof(ProgressUpdatedEvent),
                Payload = evt.Serialize(),
            };

            return envelope.Serialize();
        }

        /// <summary>
        /// Minimal in-memory <see cref="IPubSubQueue"/> so the queue-processing loop can be driven deterministically
        /// without depending on the Redis pub/sub background worker (which the integration harness intentionally disables).
        /// </summary>
        private sealed class InMemoryPubSubQueue : IPubSubQueue
        {
            // Models the Redis reliable-queue semantics: items wait on _items (head = First), a reserve moves the
            // head onto _processing, and an acknowledge removes it; a reclaim restores the processing list to the
            // queue head in order. GetNext destructively pops the queue head (used to assert the queue is drained).
            private readonly LinkedList<string?> _items;
            private readonly LinkedList<string?> _processing = new();

            public InMemoryPubSubQueue(params string?[] items)
            {
                _items = new LinkedList<string?>(items);
            }

            public string? GetNext()
            {
                if (_items.First is null)
                {
                    return null;
                }

                var value = _items.First.Value;
                _items.RemoveFirst();
                return value;
            }

            public Task<string?> GetNextAsync(CancellationToken cancellationToken = default) => Task.FromResult(GetNext());

            public Task<string?> ReserveNextAsync(CancellationToken cancellationToken = default)
            {
                if (_items.First is null)
                {
                    return Task.FromResult<string?>(null);
                }

                var value = _items.First.Value;
                _items.RemoveFirst();
                _processing.AddLast(value);
                return Task.FromResult(value);
            }

            public Task AcknowledgeAsync(string value, CancellationToken cancellationToken = default)
            {
                _processing.Remove(value);
                return Task.CompletedTask;
            }

            public Task<long> ReclaimProcessingAsync(CancellationToken cancellationToken = default)
            {
                long reclaimed = 0;
                while (_processing.Last is not null)
                {
                    _items.AddFirst(_processing.Last.Value);
                    _processing.RemoveLast();
                    reclaimed++;
                }

                return Task.FromResult(reclaimed);
            }

            public Task<long> GetLengthAsync(CancellationToken cancellationToken = default) => Task.FromResult((long)_items.Count);

            public Task<IReadOnlyList<string>> PeekAsync(long count, CancellationToken cancellationToken = default)
            {
                IReadOnlyList<string> head = count <= 0
                    ? []
                    : _items.Where(item => item is not null).Take((int)count).Cast<string>().ToList();
                return Task.FromResult(head);
            }

            public Task<bool> RemoveAsync(string value, CancellationToken cancellationToken = default)
            {
                var node = _items.Find(value);
                if (node is null)
                {
                    return Task.FromResult(false);
                }

                _items.Remove(node);
                return Task.FromResult(true);
            }

            public void AddToQueue(string value) => _items.AddLast(value);
            public Task AddToQueueAsync(string value, CancellationToken cancellationToken = default)
            {
                _items.AddLast(value);
                return Task.CompletedTask;
            }

            public Task AddRangeToQueueAsync(IEnumerable<string> values, CancellationToken cancellationToken = default)
            {
                foreach (var value in values)
                {
                    _items.AddLast(value);
                }
                return Task.CompletedTask;
            }

            // Not exercised by DataProviderSynchronizer.ProcessQueue.
            public T? GetNext<T>() => throw new NotSupportedException();
            public Task<T?> GetNextAsync<T>(CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public void AddToQueue<T>(T value) => throw new NotSupportedException();
            public Task AddToQueueAsync<T>(T value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        }

        /// <summary>
        /// An <see cref="IPubSubQueue"/> whose <see cref="ReserveNextAsync"/> delays each reserve (to widen the
        /// overlap window) and records the peak number of reserves in flight at once. A serialized drainer keeps
        /// that peak at 1; two concurrent drains would push it to 2, so it deterministically catches the #578
        /// regression. Reserved items are parked on a processing list and removed on acknowledge.
        /// </summary>
        private sealed class ConcurrencyTrackingQueue : IPubSubQueue
        {
            private readonly object _gate = new();
            private readonly Queue<string?> _items;
            private readonly List<string?> _processing = [];
            private readonly TimeSpan _reserveDelay;
            private int _activeReserves;

            public int MaxObservedConcurrency { get; private set; }

            public ConcurrencyTrackingQueue(TimeSpan reserveDelay, params string?[] items)
            {
                _reserveDelay = reserveDelay;
                _items = new Queue<string?>(items);
            }

            public async Task<string?> ReserveNextAsync(CancellationToken cancellationToken = default)
            {
                var active = Interlocked.Increment(ref _activeReserves);
                lock (_gate)
                {
                    MaxObservedConcurrency = Math.Max(MaxObservedConcurrency, active);
                }

                try
                {
                    await Task.Delay(_reserveDelay);
                    lock (_gate)
                    {
                        if (_items.Count == 0)
                        {
                            return null;
                        }

                        var value = _items.Dequeue();
                        _processing.Add(value);
                        return value;
                    }
                }
                finally
                {
                    Interlocked.Decrement(ref _activeReserves);
                }
            }

            public Task AcknowledgeAsync(string value, CancellationToken cancellationToken = default)
            {
                lock (_gate)
                {
                    _processing.Remove(value);
                }
                return Task.CompletedTask;
            }

            public Task<long> GetLengthAsync(CancellationToken cancellationToken = default)
            {
                lock (_gate)
                {
                    return Task.FromResult((long)_items.Count);
                }
            }

            // The drained-queue assertion uses GetNextAsync to confirm nothing is left waiting.
            public Task<string?> GetNextAsync(CancellationToken cancellationToken = default)
            {
                lock (_gate)
                {
                    return Task.FromResult(_items.Count > 0 ? _items.Dequeue() : null);
                }
            }

            public Task<long> ReclaimProcessingAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<IReadOnlyList<string>> PeekAsync(long count, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<bool> RemoveAsync(string value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public string? GetNext() => throw new NotSupportedException();
            public void AddToQueue(string value) => throw new NotSupportedException();
            public Task AddToQueueAsync(string value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task AddRangeToQueueAsync(IEnumerable<string> values, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public T? GetNext<T>() => throw new NotSupportedException();
            public Task<T?> GetNextAsync<T>(CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public void AddToQueue<T>(T value) => throw new NotSupportedException();
            public Task AddToQueueAsync<T>(T value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        }

        /// <summary>
        /// Wraps a real <see cref="IServiceProvider"/> but makes the first <paramref name="failuresBeforeSuccess"/>
        /// scope creations throw, simulating a transient failure that succeeds on a later retry. Scopes are created by
        /// <see cref="ServiceProviderServiceExtensions.CreateScope"/>, which resolves this provider as the
        /// <see cref="IServiceScopeFactory"/>, so failing here mirrors a transient error setting up the unit of work.
        /// </summary>
        private sealed class FlakyServiceProvider(IServiceProvider inner, int failuresBeforeSuccess) : IServiceProvider, IServiceScopeFactory
        {
            public int ScopeCreations { get; private set; }

            public object? GetService(Type serviceType)
            {
                if (serviceType == typeof(IServiceScopeFactory))
                {
                    return this;
                }

                return inner.GetService(serviceType);
            }

            public IServiceScope CreateScope()
            {
                ScopeCreations++;
                if (ScopeCreations <= failuresBeforeSuccess)
                {
                    throw new InvalidOperationException("Simulated transient failure creating a scope.");
                }

                return inner.GetRequiredService<IServiceScopeFactory>().CreateScope();
            }
        }

        /// <summary>
        /// A minimal <see cref="IPubSubService"/> stub whose <c>Subscribe</c> overloads always throw, used to verify
        /// that <see cref="DataProviderSynchronizer.StartAsync"/> propagates a subscribe failure rather than swallowing it.
        /// </summary>
        private sealed class ThrowingPubSubService : NotSupportedPubSubService
        {
            public override Task Subscribe(string channel, Action<(string message, string channel)> action, string? id = null) => throw new InvalidOperationException("Simulated subscribe failure.");
            public override Task Subscribe(string channel, string queueName, Action<(IPubSubQueue queue, string channel)> action, string id) => throw new InvalidOperationException("Simulated subscribe failure.");
            public override Task Subscribe(string channel, string queueName, Func<(IPubSubQueue queue, string channel), Task> action, string id) => throw new InvalidOperationException("Simulated subscribe failure.");
        }

        /// <summary>
        /// Wraps a real <see cref="IPubSubService"/> but makes the <c>Subscribe</c> overloads no-ops, so a test can
        /// exercise <see cref="DataProviderSynchronizer.StartAsync"/>'s startup drain against real Redis queues
        /// without registering a lingering subscription on the shared channel. Every other member delegates to the
        /// real service so the queue reads/writes (including the dead-letter queue) hit Redis as normal.
        /// </summary>
        private sealed class SubscribeSuppressingPubSubService(IPubSubService inner) : IPubSubService
        {
            public Task Publish(string channel, string message, CancellationToken cancellationToken = default) => inner.Publish(channel, message, cancellationToken);
            public Task Publish(string channel, string queueName, string queueData, CancellationToken cancellationToken = default) => inner.Publish(channel, queueName, queueData, cancellationToken);
            public Task Publish<T>(string channel, string queueName, T queueData, CancellationToken cancellationToken = default) => inner.Publish(channel, queueName, queueData, cancellationToken);
            public Task PublishBatch<T>(string channel, string queueName, IEnumerable<T> queueData, CancellationToken cancellationToken = default) => inner.PublishBatch(channel, queueName, queueData, cancellationToken);
            public Task Wake(string channel) => inner.Wake(channel);
            public Task Subscribe(string channel, Action<(string message, string channel)> action, string? id = null) => Task.CompletedTask;
            public Task Subscribe(string channel, string queueName, Action<(IPubSubQueue queue, string channel)> action, string id) => Task.CompletedTask;
            public Task Subscribe(string channel, string queueName, Func<(IPubSubQueue queue, string channel), Task> action, string id) => Task.CompletedTask;
            public Task UnSubscribe(string channel) => inner.UnSubscribe(channel);
            public Task UnSubscribe(string channel, string id) => inner.UnSubscribe(channel, id);
            public IPubSubQueue GetQueue(string queueName) => inner.GetQueue(queueName);
        }

        /// <summary>
        /// An <see cref="IPubSubService"/> that exposes a single fixed queue for the player update queue (and a
        /// throwaway in-memory queue for any other name, e.g. the dead-letter queue) and treats every subscribe
        /// (and the id-scoped unsubscribe StopAsync issues) as a no-op, so a test can drive only the startup
        /// drain over a queue it fully controls.
        /// </summary>
        private sealed class SingleQueuePubSubService(IPubSubQueue playerQueue) : NotSupportedPubSubService
        {
            public override IPubSubQueue GetQueue(string queueName) =>
                queueName == Constants.PUBSUB_PLAYER_QUEUE ? playerQueue : new InMemoryPubSubQueue();

            public override Task Subscribe(string channel, Action<(string message, string channel)> action, string? id = null) => Task.CompletedTask;
            public override Task Subscribe(string channel, string queueName, Action<(IPubSubQueue queue, string channel)> action, string id) => Task.CompletedTask;
            public override Task Subscribe(string channel, string queueName, Func<(IPubSubQueue queue, string channel), Task> action, string id) => Task.CompletedTask;
            public override Task UnSubscribe(string channel, string id) => Task.CompletedTask;
        }

        /// <summary>
        /// An <see cref="IPubSubQueue"/> that parks the drain inside the acknowledge of its first item: it signals
        /// <see cref="AcknowledgeReached"/> and then awaits <see cref="ReleaseAcknowledge"/> before completing, so
        /// a test can hold the synchronizer's drain in flight (gate held) while it triggers a stop, then release
        /// it. Counts reserves so the test can confirm the drain stopped at a clean boundary instead of reserving
        /// further items.
        /// </summary>
        private sealed class GatedDrainQueue : IPubSubQueue
        {
            private readonly Queue<string> _items;
            private readonly List<string> _processing = [];
            private bool _firstAcknowledge = true;

            public TaskCompletionSource AcknowledgeReached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource ReleaseAcknowledge { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public int ReservedCount { get; private set; }

            public GatedDrainQueue(params string[] items)
            {
                _items = new Queue<string>(items);
            }

            public Task<string?> ReserveNextAsync(CancellationToken cancellationToken = default)
            {
                if (_items.Count == 0)
                {
                    return Task.FromResult<string?>(null);
                }

                ReservedCount++;
                var value = _items.Dequeue();
                _processing.Add(value);
                return Task.FromResult<string?>(value);
            }

            public async Task AcknowledgeAsync(string value, CancellationToken cancellationToken = default)
            {
                if (_firstAcknowledge)
                {
                    _firstAcknowledge = false;
                    AcknowledgeReached.SetResult();
                    await ReleaseAcknowledge.Task;
                }

                _processing.Remove(value);
            }

            public Task<long> ReclaimProcessingAsync(CancellationToken cancellationToken = default) => Task.FromResult(0L);
            public Task<long> GetLengthAsync(CancellationToken cancellationToken = default) => Task.FromResult((long)_items.Count);
            public Task<string?> GetNextAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(_items.Count > 0 ? _items.Dequeue() : null);

            public Task<IReadOnlyList<string>> PeekAsync(long count, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<bool> RemoveAsync(string value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public string? GetNext() => throw new NotSupportedException();
            public void AddToQueue(string value) => throw new NotSupportedException();
            public Task AddToQueueAsync(string value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task AddRangeToQueueAsync(IEnumerable<string> values, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public T? GetNext<T>() => throw new NotSupportedException();
            public Task<T?> GetNextAsync<T>(CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public void AddToQueue<T>(T value) => throw new NotSupportedException();
            public Task AddToQueueAsync<T>(T value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        }

        /// <summary>
        /// An <see cref="IPubSubQueue"/> whose <see cref="ReserveNextAsync"/> parks indefinitely — a stand-in for a
        /// wedged Redis round-trip — and only unwinds when the cancellation token threaded into it is cancelled. It
        /// signals <see cref="ReserveReached"/> once parked so a test can confirm the drain is genuinely blocked
        /// inside the reserve before triggering a stop.
        /// </summary>
        private sealed class WedgedReserveQueue : IPubSubQueue
        {
            public TaskCompletionSource ReserveReached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public async Task<string?> ReserveNextAsync(CancellationToken cancellationToken = default)
            {
                ReserveReached.TrySetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return null;
            }

            public Task<long> ReclaimProcessingAsync(CancellationToken cancellationToken = default) => Task.FromResult(0L);
            public Task AcknowledgeAsync(string value, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task<long> GetLengthAsync(CancellationToken cancellationToken = default) => Task.FromResult(0L);
            public Task<string?> GetNextAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

            public Task<IReadOnlyList<string>> PeekAsync(long count, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<bool> RemoveAsync(string value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public string? GetNext() => throw new NotSupportedException();
            public void AddToQueue(string value) => throw new NotSupportedException();
            public Task AddToQueueAsync(string value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task AddRangeToQueueAsync(IEnumerable<string> values, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public T? GetNext<T>() => throw new NotSupportedException();
            public Task<T?> GetNextAsync<T>(CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public void AddToQueue<T>(T value) => throw new NotSupportedException();
            public Task AddToQueueAsync<T>(T value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        }

        private sealed class CapturingLogger<T> : ILogger<T>
        {
            public List<LogEntry> Entries { get; } = [];

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
            }

            public record LogEntry(LogLevel Level, string Message, Exception? Exception);
        }
    }
}
