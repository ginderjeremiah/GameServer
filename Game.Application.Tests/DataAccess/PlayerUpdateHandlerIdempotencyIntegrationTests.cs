using Game.Core;
using Game.Core.Players.Events;
using Game.Core.Progress;
using Game.DataAccess;
using Game.DataAccess.PlayerUpdates;
using Game.DataAccess.PlayerUpdates.Handlers;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Verifies the write-behind upsert handlers stay idempotent under the queue's at-least-once read (#891):
    /// re-applying the same event — sequentially or as a concurrent double-apply across instances — converges
    /// to a single row without surfacing a unique-constraint violation. Each apply runs through its own DI
    /// scope (its own <see cref="GameContext"/>), mirroring the synchronizer's per-event scope, and the
    /// concurrent variants provoke the race the non-atomic existence-check-then-insert exposes.
    /// </summary>
    [Collection("Integration")]
    public class PlayerUpdateHandlerIdempotencyIntegrationTests : ApplicationIntegrationTestBase
    {
        // Enough concurrent applies that several pass the existence check before any insert commits, so the
        // unique-violation catch path is exercised rather than only the fast existence-check no-op.
        private const int Parallelism = 8;

        public PlayerUpdateHandlerIdempotencyIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task ItemUnlocked_AppliedTwiceSequentially_InsertsOneRow()
        {
            var playerId = await SeedPlayerAsync();
            var itemId = await SeedItemAsync();
            var evt = new ItemUnlockedEvent(playerId, itemId);

            await ApplyAsync(evt);
            await ApplyAsync(evt);

            Assert.Equal(1, await CountAsync(c => c.UnlockedItems.CountAsync(ui => ui.PlayerId == playerId && ui.ItemId == itemId, CancellationToken)));
        }

        [Fact]
        public async Task ItemUnlocked_AppliedConcurrently_InsertsOneRowWithoutThrowing()
        {
            var playerId = await SeedPlayerAsync();
            var itemId = await SeedItemAsync();

            await ApplyConcurrentlyAsync(new ItemUnlockedEvent(playerId, itemId));

            Assert.Equal(1, await CountAsync(c => c.UnlockedItems.CountAsync(ui => ui.PlayerId == playerId && ui.ItemId == itemId, CancellationToken)));
        }

        [Fact]
        public async Task SkillUnlocked_AppliedConcurrently_InsertsOneRowWithoutThrowing()
        {
            var playerId = await SeedPlayerAsync();
            var skillId = (await SeedSkillAsync());

            await ApplyConcurrentlyAsync(new SkillUnlockedEvent(playerId, skillId));

            Assert.Equal(1, await CountAsync(c => c.PlayerSkills.CountAsync(ps => ps.PlayerId == playerId && ps.SkillId == skillId, CancellationToken)));
        }

        [Fact]
        public async Task ModUnlocked_AppliedConcurrently_InsertsOneRowWithoutThrowing()
        {
            var playerId = await SeedPlayerAsync();
            var modId = await SeedModAsync();

            await ApplyConcurrentlyAsync(new ModUnlockedEvent(playerId, modId));

            Assert.Equal(1, await CountAsync(c => c.UnlockedMods.CountAsync(um => um.PlayerId == playerId && um.ItemModId == modId, CancellationToken)));
        }

        [Fact]
        public async Task LogPreferenceChanged_InsertsThenConvergesToLatestValue()
        {
            var playerId = await SeedPlayerAsync();

            await ApplyAsync(new LogPreferenceChangedEvent(playerId, ELogType.Damage, Enabled: false));
            await ApplyAsync(new LogPreferenceChangedEvent(playerId, ELogType.Damage, Enabled: true));

            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var row = Assert.Single(await context.LogPreferences.AsNoTracking()
                .Where(lp => lp.PlayerId == playerId && lp.LogTypeId == (int)ELogType.Damage)
                .ToListAsync(CancellationToken));
            Assert.True(row.Enabled);
        }

        [Fact]
        public async Task LogPreferenceChanged_AppliedConcurrently_InsertsOneRowWithoutThrowing()
        {
            var playerId = await SeedPlayerAsync();

            await ApplyConcurrentlyAsync(new LogPreferenceChangedEvent(playerId, ELogType.Damage, Enabled: true));

            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var row = Assert.Single(await context.LogPreferences.AsNoTracking()
                .Where(lp => lp.PlayerId == playerId && lp.LogTypeId == (int)ELogType.Damage)
                .ToListAsync(CancellationToken));
            Assert.True(row.Enabled);
        }

        [Fact]
        public async Task ProgressUpdated_AppliedTwice_UpsertsStatisticsAndChallengesWithoutDuplicating()
        {
            var playerId = await SeedPlayerAsync();
            int challengeId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                challengeId = (await TestDataSeeder.CreateChallengeAsync(context)).Id;
            }

            var first = MakeProgressEvent(playerId, challengeId, statValue: 5m, challengeProgress: 5m, completed: false);
            await ApplyAsync(first);

            // The second apply carries higher absolute values: the existing rows are updated in place, not
            // duplicated, so progress converges to the latest snapshot.
            var second = MakeProgressEvent(playerId, challengeId, statValue: 9m, challengeProgress: 9m, completed: true);
            await ApplyAsync(second);

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var stat = Assert.Single(await verifyContext.PlayerStatistics.AsNoTracking()
                .Where(ps => ps.PlayerId == playerId && ps.StatisticTypeId == (int)EStatisticType.EnemiesKilled)
                .ToListAsync(CancellationToken));
            Assert.Equal(9m, stat.Value);

            var challenge = Assert.Single(await verifyContext.PlayerChallenges.AsNoTracking()
                .Where(pc => pc.PlayerId == playerId && pc.ChallengeId == challengeId)
                .ToListAsync(CancellationToken));
            Assert.Equal(9m, challenge.Progress);
            Assert.True(challenge.Completed);
        }

        [Fact]
        public async Task ProgressUpdated_AppliedConcurrently_InsertsOneRowPerKeyWithoutThrowing()
        {
            var playerId = await SeedPlayerAsync();
            int challengeId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                challengeId = (await TestDataSeeder.CreateChallengeAsync(context)).Id;
            }

            await ApplyConcurrentlyAsync(MakeProgressEvent(playerId, challengeId, statValue: 7m, challengeProgress: 7m, completed: false));

            Assert.Equal(1, await CountAsync(c => c.PlayerStatistics.CountAsync(ps => ps.PlayerId == playerId && ps.StatisticTypeId == (int)EStatisticType.EnemiesKilled, CancellationToken)));
            Assert.Equal(1, await CountAsync(c => c.PlayerChallenges.CountAsync(pc => pc.PlayerId == playerId && pc.ChallengeId == challengeId, CancellationToken)));
        }

        [Fact]
        public async Task AttributeAllocationsChanged_AppliedTwice_UpsertsAllocationsWithoutDuplicating()
        {
            var playerId = await SeedPlayerAsync();

            // Intellect has no seeded row (insert), Strength does (update). The second apply carries different
            // amounts: the existing rows are updated in place, not duplicated, so allocations converge.
            await ApplyAsync(MakeAttributeEvent(playerId, intellect: 10d, strength: 20d));
            await ApplyAsync(MakeAttributeEvent(playerId, intellect: 30d, strength: 40d));

            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var intellect = Assert.Single(await context.PlayerAttributes.AsNoTracking()
                .Where(pa => pa.PlayerId == playerId && pa.AttributeId == (int)EAttribute.Intellect)
                .ToListAsync(CancellationToken));
            Assert.Equal(30m, intellect.Amount);
            var strength = Assert.Single(await context.PlayerAttributes.AsNoTracking()
                .Where(pa => pa.PlayerId == playerId && pa.AttributeId == (int)EAttribute.Strength)
                .ToListAsync(CancellationToken));
            Assert.Equal(40m, strength.Amount);
        }

        [Fact]
        public async Task AttributeAllocationsChanged_AppliedConcurrently_InsertsOneRowPerAttributeWithoutThrowing()
        {
            var playerId = await SeedPlayerAsync();

            // Intellect starts with no row, so the concurrent applies race on its insert; the clear-and-re-apply
            // path resolves the conflict to an update on the second pass rather than throwing.
            await ApplyConcurrentlyAsync(MakeAttributeEvent(playerId, intellect: 15d, strength: 25d));

            Assert.Equal(1, await CountAsync(c => c.PlayerAttributes.CountAsync(pa => pa.PlayerId == playerId && pa.AttributeId == (int)EAttribute.Intellect, CancellationToken)));
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var intellect = Assert.Single(await context.PlayerAttributes.AsNoTracking()
                .Where(pa => pa.PlayerId == playerId && pa.AttributeId == (int)EAttribute.Intellect)
                .ToListAsync(CancellationToken));
            Assert.Equal(15m, intellect.Amount);
        }

        [Fact]
        public async Task ModApplied_AppliedTwice_ConvergesToOneRowWithLatestMod()
        {
            var (playerId, itemId, slotId, firstModId) = await SeedAppliedModFixtureAsync();
            int secondModId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                secondModId = (await TestDataSeeder.CreateItemModAsync(context, name: "Second Mod")).Id;
            }

            // The second apply swaps the slot's mod: the delete-then-insert replaces the row in place rather
            // than duplicating, so the slot converges to the latest mod.
            await ApplyAsync(new ModAppliedEvent(playerId, itemId, slotId, firstModId));
            await ApplyAsync(new ModAppliedEvent(playerId, itemId, slotId, secondModId));

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var row = Assert.Single(await verifyContext.AppliedMods.AsNoTracking()
                .Where(am => am.PlayerId == playerId && am.ItemId == itemId && am.ItemModSlotId == slotId)
                .ToListAsync(CancellationToken));
            Assert.Equal(secondModId, row.ItemModId);
        }

        [Fact]
        public async Task ModApplied_AppliedConcurrently_InsertsOneRowWithoutThrowing()
        {
            var (playerId, itemId, slotId, modId) = await SeedAppliedModFixtureAsync();

            await ApplyConcurrentlyAsync(new ModAppliedEvent(playerId, itemId, slotId, modId));

            Assert.Equal(1, await CountAsync(c => c.AppliedMods.CountAsync(am => am.PlayerId == playerId && am.ItemId == itemId && am.ItemModSlotId == slotId, CancellationToken)));
        }

        [Fact]
        public async Task SelectedSkillsChanged_SkillRowMissing_InsertsSelectedRowInsteadOfDropping()
        {
            // Models a SelectedSkillsChangedEvent reordered ahead of the skill's SkillUnlockedEvent: the
            // skill row doesn't exist yet, so the pre-fix handler silently left it unselected.
            var playerId = await SeedPlayerAsync();
            var skillId = await SeedSkillAsync();

            await ApplyAsync(new SelectedSkillsChangedEvent(playerId, [skillId]));

            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var row = Assert.Single(await context.PlayerSkills.AsNoTracking()
                .Where(ps => ps.PlayerId == playerId && ps.SkillId == skillId)
                .ToListAsync(CancellationToken));
            Assert.True(row.Selected);
            Assert.Equal(0, row.Order);
        }

        [Fact]
        public async Task SelectedSkillsChanged_AppliedTwice_ConvergesToOrderedLoadoutWithoutDuplicating()
        {
            var playerId = await SeedPlayerAsync();
            var firstSkillId = await SeedSkillAsync();
            var secondSkillId = await SeedSkillAsync();
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                // Both skills already unlocked but unselected (the common in-order case).
                await TestDataSeeder.LinkSkillToPlayerAsync(context, playerId, firstSkillId, selected: false);
                await TestDataSeeder.LinkSkillToPlayerAsync(context, playerId, secondSkillId, selected: false);
            }

            await ApplyAsync(new SelectedSkillsChangedEvent(playerId, [secondSkillId, firstSkillId]));
            // Re-apply with a different order: existing rows are updated in place, not duplicated.
            await ApplyAsync(new SelectedSkillsChangedEvent(playerId, [firstSkillId]));

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var rows = await verifyContext.PlayerSkills.AsNoTracking()
                .Where(ps => ps.PlayerId == playerId)
                .ToListAsync(CancellationToken);
            Assert.Equal(2, rows.Count);
            var first = Assert.Single(rows, r => r.SkillId == firstSkillId);
            Assert.True(first.Selected);
            Assert.Equal(0, first.Order);
            var second = Assert.Single(rows, r => r.SkillId == secondSkillId);
            Assert.False(second.Selected);
            Assert.Equal(0, second.Order);
        }

        [Fact]
        public async Task SelectedSkillsChanged_AppliedConcurrently_InsertsOneRowWithoutThrowing()
        {
            // The skill has no row, so the concurrent applies race on its insert; the clear-and-re-apply path
            // resolves the conflict to an update on the second pass rather than throwing.
            var playerId = await SeedPlayerAsync();
            var skillId = await SeedSkillAsync();

            await ApplyConcurrentlyAsync(new SelectedSkillsChangedEvent(playerId, [skillId]));

            Assert.Equal(1, await CountAsync(c => c.PlayerSkills.CountAsync(ps => ps.PlayerId == playerId && ps.SkillId == skillId, CancellationToken)));
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var row = Assert.Single(await context.PlayerSkills.AsNoTracking()
                .Where(ps => ps.PlayerId == playerId && ps.SkillId == skillId)
                .ToListAsync(CancellationToken));
            Assert.True(row.Selected);
        }

        [Fact]
        public async Task ItemEquipped_ItemRowMissing_InsertsEquippedRowInsteadOfLeavingSlotEmpty()
        {
            // Models an ItemEquippedEvent reordered ahead of the item's ItemUnlockedEvent: the unlocked-item
            // row doesn't exist yet, so the pre-fix ExecuteUpdate matched zero rows and left the slot empty.
            var playerId = await SeedPlayerAsync();
            var itemId = await SeedItemAsync();

            await ApplyAsync(new ItemEquippedEvent(playerId, itemId, (int)EEquipmentSlot.HelmSlot));

            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var row = Assert.Single(await context.UnlockedItems.AsNoTracking()
                .Where(ui => ui.PlayerId == playerId && ui.ItemId == itemId)
                .ToListAsync(CancellationToken));
            Assert.Equal((int)EEquipmentSlot.HelmSlot, row.EquipmentSlotId);
        }

        [Fact]
        public async Task ItemEquipped_AppliedTwice_ClearsPriorOccupantAndConvergesWithoutDuplicating()
        {
            var playerId = await SeedPlayerAsync();
            var incumbentId = await SeedItemAsync();
            var newItemId = await SeedItemAsync();
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                // The slot is already occupied by the incumbent; the new item is unlocked but unequipped.
                await TestDataSeeder.LinkItemToPlayerAsync(context, playerId, incumbentId, EEquipmentSlot.HelmSlot);
                await TestDataSeeder.LinkItemToPlayerAsync(context, playerId, newItemId);
            }

            var evt = new ItemEquippedEvent(playerId, newItemId, (int)EEquipmentSlot.HelmSlot);
            await ApplyAsync(evt);
            await ApplyAsync(evt);

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var newRow = Assert.Single(await verifyContext.UnlockedItems.AsNoTracking()
                .Where(ui => ui.PlayerId == playerId && ui.ItemId == newItemId)
                .ToListAsync(CancellationToken));
            Assert.Equal((int)EEquipmentSlot.HelmSlot, newRow.EquipmentSlotId);
            var incumbentRow = Assert.Single(await verifyContext.UnlockedItems.AsNoTracking()
                .Where(ui => ui.PlayerId == playerId && ui.ItemId == incumbentId)
                .ToListAsync(CancellationToken));
            Assert.Null(incumbentRow.EquipmentSlotId);
        }

        [Fact]
        public async Task ItemEquipped_AppliedConcurrently_InsertsOneRowWithoutThrowing()
        {
            // The item has no unlocked-item row, so the concurrent applies race on its insert; the
            // clear-and-re-apply path resolves the conflict to an update on the second pass rather than throwing.
            var playerId = await SeedPlayerAsync();
            var itemId = await SeedItemAsync();

            await ApplyConcurrentlyAsync(new ItemEquippedEvent(playerId, itemId, (int)EEquipmentSlot.HelmSlot));

            Assert.Equal(1, await CountAsync(c => c.UnlockedItems.CountAsync(ui => ui.PlayerId == playerId && ui.ItemId == itemId, CancellationToken)));
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var row = Assert.Single(await context.UnlockedItems.AsNoTracking()
                .Where(ui => ui.PlayerId == playerId && ui.ItemId == itemId)
                .ToListAsync(CancellationToken));
            Assert.Equal((int)EEquipmentSlot.HelmSlot, row.EquipmentSlotId);
        }

        [Fact]
        public async Task ItemUnequipped_AppliedTwice_ClearsSlotAndConverges()
        {
            var playerId = await SeedPlayerAsync();
            var itemId = await SeedItemAsync();
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                await TestDataSeeder.LinkItemToPlayerAsync(context, playerId, itemId, EEquipmentSlot.HelmSlot);
            }

            var evt = new ItemUnequippedEvent(playerId, itemId);
            await ApplyAsync(evt);
            await ApplyAsync(evt);

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var row = Assert.Single(await verifyContext.UnlockedItems.AsNoTracking()
                .Where(ui => ui.PlayerId == playerId && ui.ItemId == itemId)
                .ToListAsync(CancellationToken));
            Assert.Null(row.EquipmentSlotId);
        }

        [Fact]
        public async Task ItemUnequipped_ItemRowMissingThenUnlockArrives_ConvergesToUnequipped()
        {
            // Models an ItemUnequippedEvent reordered ahead of the item's ItemUnlockedEvent: the unequip is a
            // benign no-op against the missing row, and the later unlock inserts it with a null (unequipped)
            // slot — so "unequipped" still converges without the unequip handler needing an insert.
            var playerId = await SeedPlayerAsync();
            var itemId = await SeedItemAsync();

            await ApplyAsync(new ItemUnequippedEvent(playerId, itemId));
            await ApplyAsync(new ItemUnlockedEvent(playerId, itemId));

            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var row = Assert.Single(await context.UnlockedItems.AsNoTracking()
                .Where(ui => ui.PlayerId == playerId && ui.ItemId == itemId)
                .ToListAsync(CancellationToken));
            Assert.Null(row.EquipmentSlotId);
        }

        [Fact]
        public async Task ItemFavoriteChanged_ItemRowMissing_InsertsFavoritedRowInsteadOfDropping()
        {
            // Models an ItemFavoriteChangedEvent reordered ahead of the item's ItemUnlockedEvent: the
            // unlocked-item row doesn't exist yet, so the pre-fix ExecuteUpdate matched zero rows and the
            // favorite was silently dropped until a later edit self-healed the DB.
            var playerId = await SeedPlayerAsync();
            var itemId = await SeedItemAsync();

            await ApplyAsync(new ItemFavoriteChangedEvent(playerId, itemId, Favorite: true));

            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var row = Assert.Single(await context.UnlockedItems.AsNoTracking()
                .Where(ui => ui.PlayerId == playerId && ui.ItemId == itemId)
                .ToListAsync(CancellationToken));
            Assert.True(row.Favorite);
            Assert.Null(row.EquipmentSlotId);
        }

        [Fact]
        public async Task ItemFavoriteChanged_RowMissingThenUnlockArrives_ConvergesToFavoritedWithoutDuplicating()
        {
            // The favorite inserts the row early; the later unlock must no-op on it rather than duplicate or
            // clear the flag, so the player keeps the favorite they set.
            var playerId = await SeedPlayerAsync();
            var itemId = await SeedItemAsync();

            await ApplyAsync(new ItemFavoriteChangedEvent(playerId, itemId, Favorite: true));
            await ApplyAsync(new ItemUnlockedEvent(playerId, itemId));

            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var row = Assert.Single(await context.UnlockedItems.AsNoTracking()
                .Where(ui => ui.PlayerId == playerId && ui.ItemId == itemId)
                .ToListAsync(CancellationToken));
            Assert.True(row.Favorite);
        }

        [Fact]
        public async Task ItemFavoriteChanged_AppliedTwice_ConvergesToLatestFlagWithoutDuplicating()
        {
            var playerId = await SeedPlayerAsync();
            var itemId = await SeedItemAsync();
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                // The item is already unlocked and favorited (the common in-order case).
                await TestDataSeeder.LinkItemToPlayerAsync(context, playerId, itemId, favorite: true);
            }

            // Re-apply with the opposite value: the existing row is updated in place, not duplicated.
            await ApplyAsync(new ItemFavoriteChangedEvent(playerId, itemId, Favorite: false));

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var row = Assert.Single(await verifyContext.UnlockedItems.AsNoTracking()
                .Where(ui => ui.PlayerId == playerId && ui.ItemId == itemId)
                .ToListAsync(CancellationToken));
            Assert.False(row.Favorite);
        }

        [Fact]
        public async Task ItemFavoriteChanged_AppliedConcurrently_InsertsOneRowWithoutThrowing()
        {
            // The item has no unlocked-item row, so the concurrent applies race on its insert; the
            // clear-and-re-apply path resolves the conflict to an update on the second pass rather than throwing.
            var playerId = await SeedPlayerAsync();
            var itemId = await SeedItemAsync();

            await ApplyConcurrentlyAsync(new ItemFavoriteChangedEvent(playerId, itemId, Favorite: true));

            Assert.Equal(1, await CountAsync(c => c.UnlockedItems.CountAsync(ui => ui.PlayerId == playerId && ui.ItemId == itemId, CancellationToken)));
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var row = Assert.Single(await context.UnlockedItems.AsNoTracking()
                .Where(ui => ui.PlayerId == playerId && ui.ItemId == itemId)
                .ToListAsync(CancellationToken));
            Assert.True(row.Favorite);
        }

        [Fact]
        public async Task PlayerCoreUpdated_AppliedTwice_ConvergesToLatestValuesWithoutDuplicating()
        {
            var playerId = await SeedPlayerAsync();
            var zoneId = await SeedZoneAsync();

            // Update-only against the always-present Players row: re-applying with higher absolute values
            // updates the row in place rather than duplicating, so the core fields converge to the latest.
            await ApplyAsync(new PlayerCoreUpdatedEvent(playerId, Level: 7, Exp: 120, CurrentZoneId: zoneId, StatPointsGained: 130, StatPointsUsed: 110));
            await ApplyAsync(new PlayerCoreUpdatedEvent(playerId, Level: 9, Exp: 250, CurrentZoneId: zoneId, StatPointsGained: 160, StatPointsUsed: 140));

            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var row = Assert.Single(await context.Players.AsNoTracking()
                .Where(p => p.Id == playerId)
                .ToListAsync(CancellationToken));
            Assert.Equal(9, row.Level);
            Assert.Equal(250, row.Exp);
            Assert.Equal(zoneId, row.CurrentZoneId);
            Assert.Equal(160, row.StatPointsGained);
            Assert.Equal(140, row.StatPointsUsed);
        }

        [Fact]
        public async Task ModRemoved_AppliedTwice_DeletesTheSlotsModAndIsIdempotent()
        {
            var (playerId, itemId, slotId, modId) = await SeedAppliedModFixtureAsync();
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                await TestDataSeeder.ApplyModToItemAsync(context, playerId, itemId, slotId, modId);
            }

            // The first apply deletes the slot's mod; the second is a benign no-op against the now-missing
            // row rather than throwing, so a re-delivered event converges to the same empty slot.
            var evt = new ModRemovedEvent(playerId, itemId, slotId);
            await ApplyAsync(evt);
            await ApplyAsync(evt);

            Assert.Equal(0, await CountAsync(c => c.AppliedMods.CountAsync(am => am.PlayerId == playerId && am.ItemId == itemId && am.ItemModSlotId == slotId, CancellationToken)));
        }

        [Fact]
        public async Task ModRemoved_RowMissing_IsNoOp()
        {
            // No AppliedMod row exists for the slot (e.g. the remove reordered ahead of the apply, or a
            // re-delivery after the row is already gone): the delete must match zero rows without throwing.
            var (playerId, itemId, slotId, _) = await SeedAppliedModFixtureAsync();

            await ApplyAsync(new ModRemovedEvent(playerId, itemId, slotId));

            Assert.Equal(0, await CountAsync(c => c.AppliedMods.CountAsync(am => am.PlayerId == playerId && am.ItemId == itemId && am.ItemModSlotId == slotId, CancellationToken)));
        }

        private static AttributeAllocationsChangedEvent MakeAttributeEvent(int playerId, double intellect, double strength) => new(
            playerId,
            [
                new AttributeAllocationEntry(EAttribute.Intellect, intellect),
                new AttributeAllocationEntry(EAttribute.Strength, strength),
            ]);

        // Seeds an item with a mod slot plus a mod the player has unlocked, the prerequisites a ModAppliedEvent
        // assumes already exist.
        private async Task<(int playerId, int itemId, int slotId, int modId)> SeedAppliedModFixtureAsync()
        {
            var playerId = await SeedPlayerAsync();
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var itemId = (await TestDataSeeder.CreateItemAsync(context)).Id;
            var slotId = (await TestDataSeeder.AddItemModSlotAsync(context, itemId)).Id;
            var modId = (await TestDataSeeder.CreateItemModAsync(context)).Id;
            await TestDataSeeder.LinkItemToPlayerAsync(context, playerId, itemId);
            await TestDataSeeder.LinkModToPlayerAsync(context, playerId, modId);
            return (playerId, itemId, slotId, modId);
        }

        // Deterministic guard for the invariant the concurrent-apply test above relies on but can only
        // provoke under a timing race (which a fast, idle machine rarely hits). EntityId is null for global
        // statistics, and a default Postgres unique index treats nulls as distinct — so two
        // (player, type, null) rows would silently coexist, defeating the handler's
        // unique-violation-then-retry idempotency. NULLS NOT DISTINCT must make the second insert collide.
        [Fact]
        public async Task PlayerStatistics_DuplicateGlobalStatistic_RejectedByUniqueIndex()
        {
            var playerId = await SeedPlayerAsync();

            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                context.PlayerStatistics.Add(NewGlobalEnemiesKilled(playerId, 1m));
                await context.SaveChangesAsync(CancellationToken);
            }

            using var dupScope = CreateScope();
            var dupContext = dupScope.ServiceProvider.GetRequiredService<GameContext>();
            dupContext.PlayerStatistics.Add(NewGlobalEnemiesKilled(playerId, 2m));

            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => dupContext.SaveChangesAsync(CancellationToken));
            Assert.True(ex.IsUniqueViolation());
        }

        private static Infrastructure.Entities.PlayerStatistic NewGlobalEnemiesKilled(int playerId, decimal value) => new()
        {
            PlayerId = playerId,
            StatisticTypeId = (int)EStatisticType.EnemiesKilled,
            EntityId = null,
            Value = value,
        };

        private static ProgressUpdatedEvent MakeProgressEvent(int playerId, int challengeId, decimal statValue, decimal challengeProgress, bool completed) => new()
        {
            PlayerId = playerId,
            Statistics = [new CachedPlayerStatistic { StatisticTypeId = (int)EStatisticType.EnemiesKilled, EntityId = null, Value = statValue }],
            Challenges = [new CachedPlayerChallenge { ChallengeId = challengeId, Progress = challengeProgress, Completed = completed, CompletedAt = completed ? DateTime.UtcNow : null }],
        };

        private async Task ApplyAsync<TEvent>(TEvent evt)
        {
            using var scope = CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<IPlayerUpdateHandler<TEvent>>();
            await handler.HandleAsync(evt);
        }

        // Runs the same event through several independent scopes at once so multiple applies pass the
        // existence check before any insert commits — the cross-instance double-apply the catch absorbs.
        private async Task ApplyConcurrentlyAsync<TEvent>(TEvent evt)
        {
            var scopes = Enumerable.Range(0, Parallelism).Select(_ => CreateScope()).ToList();
            try
            {
                var handlers = scopes.Select(s => s.ServiceProvider.GetRequiredService<IPlayerUpdateHandler<TEvent>>()).ToList();
                await Task.WhenAll(handlers.Select(h => Task.Run(() => h.HandleAsync(evt))));
            }
            finally
            {
                foreach (var scope in scopes)
                {
                    scope.Dispose();
                }
            }
        }

        private async Task<int> CountAsync(Func<GameContext, Task<int>> count)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            return await count(context);
        }

        private async Task<int> SeedPlayerAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            return player.Id;
        }

        private async Task<int> SeedItemAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            return (await TestDataSeeder.CreateItemAsync(context)).Id;
        }

        private async Task<int> SeedSkillAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            return (await TestDataSeeder.CreateSkillAsync(context)).Id;
        }

        private async Task<int> SeedModAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            return (await TestDataSeeder.CreateItemModAsync(context)).Id;
        }

        private async Task<int> SeedZoneAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            return (await TestDataSeeder.CreateZoneAsync(context)).Id;
        }
    }
}
