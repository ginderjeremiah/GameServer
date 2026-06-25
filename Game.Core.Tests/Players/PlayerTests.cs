using Game.Core.Attributes.Modifiers;
using Game.Core.Battle;
using Game.Core.Battle.Events;
using Game.Core.Enemies;
using Game.Core.Items;
using Game.Core.Players;
using Game.Core.Players.Events;
using Game.Core.Players.Inventories;
using Game.Core.Skills;
using Game.Core.TestInfrastructure.Builders;
using Xunit;

namespace Game.Core.Tests.Players
{
    public class PlayerTests
    {
        // ── GrantExp ─────────────────────────────────────────────────────────

        [Fact]
        public void GrantExp_BelowLevelThreshold_DoesNotLevelUp()
        {
            var player = MakePlayer(level: 1, exp: 0);

            player.GrantExp(50); // threshold is 1 * 100 = 100

            Assert.Equal(1, player.Level);
            Assert.Equal(50, player.Exp);
        }

        [Fact]
        public void GrantExp_ReachesThreshold_IncrementsLevel()
        {
            var player = MakePlayer(level: 1, exp: 0);

            player.GrantExp(101);

            Assert.Equal(2, player.Level);
            Assert.Equal(1, player.Exp);        // 101 - 100 = 1 carried over
        }

        [Fact]
        public void GrantExp_OnLevelUp_RaisesPlayerLeveledUpEvent()
        {
            var player = MakePlayer(level: 1, exp: 0);

            player.GrantExp(101);

            var evt = player.DomainEvents.OfType<PlayerLeveledUpEvent>().SingleOrDefault();
            Assert.NotNull(evt);
            Assert.Equal(player, evt.Player);
            Assert.Equal(2, evt.NewLevel);
        }

        [Fact]
        public void GrantExp_NoLevelUp_OnlyCoreUpdatedEvent()
        {
            var player = MakePlayer(level: 1, exp: 0);

            player.GrantExp(50);

            Assert.Single(player.DomainEvents);
            Assert.IsType<PlayerCoreUpdatedEvent>(player.DomainEvents[0]);
        }

        [Fact]
        public void GrantExp_LevelUp_GrantsTheFreePoolStatPoints()
        {
            var player = MakePlayer(level: 1, exp: 0);
            var before = player.StatPoints.StatPointsGained;

            player.GrantExp(101);

            // One level grants one free pool's worth of stat points (the reduced per-level grant; the locked
            // base supplies the rest of the attribute growth — #1223).
            Assert.Equal(before + GameConstants.StatPointsPerLevel, player.StatPoints.StatPointsGained);
        }

        // ── GrantExp — multi-level-up ────────────────────────────────────────

        [Fact]
        public void GrantExp_EnoughForTwoLevels_LevelsTwice()
        {
            var player = MakePlayer(level: 1, exp: 0);

            player.GrantExp(301); // threshold 100 (lvl1) + 200 (lvl2) = 300 to reach lvl3

            Assert.Equal(3, player.Level);
            Assert.Equal(1, player.Exp);
        }

        [Fact]
        public void GrantExp_MultiLevelUp_RaisesOneEventPerLevel()
        {
            var player = MakePlayer(level: 1, exp: 0);

            player.GrantExp(301);

            var events = player.DomainEvents.OfType<PlayerLeveledUpEvent>().ToList();
            Assert.Equal(2, events.Count);
            Assert.Equal(2, events[0].NewLevel);
            Assert.Equal(3, events[1].NewLevel);
        }

        [Fact]
        public void GrantExp_MultiLevelUp_AccumulatesStatPoints()
        {
            var player = MakePlayer(level: 1, exp: 0);

            player.GrantExp(301);

            // Two levels' worth of the per-level free pool grant.
            Assert.Equal(2 * GameConstants.StatPointsPerLevel, player.StatPoints.StatPointsGained);
        }

        [Fact]
        public void GrantExp_ExactlyAtThreshold_LevelsUp()
        {
            var player = MakePlayer(level: 1, exp: 0);

            player.GrantExp(100); // threshold is >+ 100, not >

            Assert.Equal(2, player.Level);
            Assert.Equal(0, player.Exp);
        }

        // ── GrantExp — bounded against pathological input ────────────────────

        [Fact]
        public void GrantExp_HugeGrant_ClampsExpAndBoundsTheLevelUpLoop()
        {
            var player = MakePlayer(level: 1, exp: 0);

            // A tampered/replayed value far beyond MaxExpPerGrant: the grant is clamped to the ceiling,
            // so it applies the same as MaxExpPerGrant and produces a finite, bounded set of level-ups
            // rather than spinning the loop on int.MaxValue.
            player.GrantExp(int.MaxValue);

            var clamped = MakePlayer(level: 1, exp: 0);
            clamped.GrantExp(ServerGameConstants.MaxExpPerGrant);

            Assert.Equal(clamped.Level, player.Level);
            Assert.Equal(clamped.Exp, player.Exp);
            // The event burst is bounded: one event per level gained, with the same finite count both ways.
            Assert.Equal(
                clamped.DomainEvents.OfType<PlayerLeveledUpEvent>().Count(),
                player.DomainEvents.OfType<PlayerLeveledUpEvent>().Count());
        }

        [Fact]
        public void GrantExp_MaxExpPerGrant_ProducesBoundedLevelUpCount()
        {
            var player = MakePlayer(level: 1, exp: 0);

            player.GrantExp(ServerGameConstants.MaxExpPerGrant);

            // The per-level cost grows linearly (Level * ExpPerLevel), so a clamped grant can only ever
            // produce a small, finite number of levels — proving the loop is bounded rather than the
            // amount/ExpPerLevel runaway an unclamped grant would allow.
            var levelUps = player.DomainEvents.OfType<PlayerLeveledUpEvent>().Count();
            Assert.True(levelUps is > 0 and < 100, $"Expected a bounded level-up count, got {levelUps}.");
        }

        [Fact]
        public void GrantExp_NegativeAmount_IsClampedToZeroAndDoesNotReduceExp()
        {
            var player = MakePlayer(level: 2, exp: 50);

            player.GrantExp(-1000);

            // A negative grant is clamped to 0, so exp is unchanged and no level/de-level occurs.
            Assert.Equal(2, player.Level);
            Assert.Equal(50, player.Exp);
            Assert.Empty(player.DomainEvents.OfType<PlayerLeveledUpEvent>());
        }

        [Fact]
        public void GrantExp_CorruptZeroLevel_FailsFastInsteadOfSpinning()
        {
            // Level is required and ≥ 1 on every real construction path, so a Level of 0 is a corrupt
            // aggregate. The grant must throw loudly rather than tolerate a zero level-up threshold.
            var player = MakePlayer(level: 0, exp: 0);

            Assert.Throws<InvalidOperationException>(() => player.GrantExp(100));
        }

        // ── UnlockItem ──────────────────────────────────────────────────────

        [Fact]
        public void UnlockItem_AddsItemToInventory()
        {
            var player = MakePlayer();
            var item = MakeItem(id: 10);

            player.UnlockItem(item);

            Assert.Single(player.Inventory.UnlockedItems);
            Assert.Equal(item, player.Inventory.UnlockedItems.Single().Item);
        }

        [Fact]
        public void UnlockItem_RaisesItemUnlockedEvent()
        {
            var player = MakePlayer();
            var item = MakeItem(id: 10);

            player.UnlockItem(item);

            var evt = player.DomainEvents.OfType<ItemUnlockedEvent>().SingleOrDefault();
            Assert.NotNull(evt);
            Assert.Equal(player.Id, evt.PlayerId);
            Assert.Equal(10, evt.ItemId);
        }

        // ── UnlockMod ───────────────────────────────────────────────────────

        [Fact]
        public void UnlockMod_AddsModToInventory()
        {
            var player = MakePlayer();

            player.UnlockMod(5);

            Assert.Contains(5, player.Inventory.UnlockedMods);
        }

        [Fact]
        public void UnlockMod_RaisesModUnlockedEvent()
        {
            var player = MakePlayer();

            player.UnlockMod(5);

            var evt = player.DomainEvents.OfType<ModUnlockedEvent>().SingleOrDefault();
            Assert.NotNull(evt);
            Assert.Equal(player.Id, evt.PlayerId);
            Assert.Equal(5, evt.ItemModId);
        }

        // ── UnlockSkill ──────────────────────────────────────────────────────

        [Fact]
        public void UnlockSkill_AddsSkillToUnlockedSetUnselected()
        {
            var player = MakePlayer();
            var skill = MakeSkill(id: 7);

            player.UnlockSkill(skill);

            Assert.Equal(skill, Assert.Single(player.Skills));
            // Earning a skill does not equip it.
            Assert.Empty(player.SelectedSkills);
        }

        [Fact]
        public void UnlockSkill_RaisesSkillUnlockedEvent()
        {
            var player = MakePlayer();
            var skill = MakeSkill(id: 7);

            player.UnlockSkill(skill);

            var evt = player.DomainEvents.OfType<SkillUnlockedEvent>().SingleOrDefault();
            Assert.NotNull(evt);
            Assert.Equal(player.Id, evt.PlayerId);
            Assert.Equal(7, evt.SkillId);
        }

        [Fact]
        public void UnlockSkill_AlreadyUnlocked_DoesNotDuplicateInUnlockedSet()
        {
            var player = MakePlayer();
            player.UnlockSkill(MakeSkill(id: 7));

            player.UnlockSkill(MakeSkill(id: 7));

            Assert.Single(player.Skills);
        }

        // ── Idempotent unlocks raise no event on the no-op path ──────────────

        [Fact]
        public void UnlockItem_AlreadyUnlocked_DoesNotRaiseSecondEvent()
        {
            var player = MakePlayer();
            var item = MakeItem(id: 10);
            player.UnlockItem(item);
            player.ClearEvents();

            player.UnlockItem(item);

            // Re-granting an already-owned item is a no-op: no redundant write-behind work or client notice.
            Assert.Empty(player.DomainEvents.OfType<ItemUnlockedEvent>());
        }

        [Fact]
        public void UnlockMod_AlreadyUnlocked_DoesNotRaiseSecondEvent()
        {
            var player = MakePlayer();
            player.UnlockMod(5);
            player.ClearEvents();

            player.UnlockMod(5);

            Assert.Empty(player.DomainEvents.OfType<ModUnlockedEvent>());
        }

        [Fact]
        public void UnlockSkill_AlreadyUnlocked_DoesNotRaiseSecondEvent()
        {
            var player = MakePlayer();
            player.UnlockSkill(MakeSkill(id: 7));
            player.ClearEvents();

            player.UnlockSkill(MakeSkill(id: 7));

            Assert.Empty(player.DomainEvents.OfType<SkillUnlockedEvent>());
        }

        // ── GrantOfflineExp (batched offline grants) ─────────────────────────

        [Fact]
        public void GrantOfflineExp_SumsRewardsAndLevelsUp()
        {
            var player = MakePlayer(level: 1, exp: 0);

            // 50 + 60 = 110 ≥ the level-1 threshold of 100, so the player reaches level 2 with 10 carried.
            player.GrantOfflineExp([50, 60]);

            Assert.Equal(2, player.Level);
            Assert.Equal(10, player.Exp);
        }

        [Fact]
        public void GrantOfflineExp_RaisesSingleCoreUpdatedEventForTheWholeBatch()
        {
            var player = MakePlayer(level: 1, exp: 0);

            // Many victories, but the batch raises exactly one core-updated event (vs one per grant) so the
            // offline window doesn't flood the write-behind queue (spike #879 decision 6).
            player.GrantOfflineExp([10, 10, 10, 10, 10]);

            Assert.Single(player.DomainEvents.OfType<PlayerCoreUpdatedEvent>());
        }

        [Fact]
        public void GrantOfflineExp_AppliesPerGrantClampNotSumClamp()
        {
            var clampedPerGrant = MakePlayer(level: 1, exp: 0);
            var clampedAsSum = MakePlayer(level: 1, exp: 0);

            // Two max grants sum to twice the per-grant clamp; applied per-grant (offline batch) the full
            // amount lands, whereas a single grant of the sum would be clamped to one MaxExpPerGrant. So the
            // batched path must out-level the single-clamped grant — proving each reward clears the clamp.
            clampedPerGrant.GrantOfflineExp([ServerGameConstants.MaxExpPerGrant, ServerGameConstants.MaxExpPerGrant]);
            clampedAsSum.GrantExp(2 * ServerGameConstants.MaxExpPerGrant);

            Assert.True(clampedPerGrant.Level > clampedAsSum.Level);
        }

        [Fact]
        public void GrantOfflineExp_RaisesOneLevelUpEventPerLevel()
        {
            var player = MakePlayer(level: 1, exp: 0);

            // 110 + 200 = 310 ≥ 100 (lvl1) + 200 (lvl2) = 300, so two levels are gained across the batch.
            player.GrantOfflineExp([110, 200]);

            Assert.Equal(3, player.Level);
            Assert.Equal(2, player.DomainEvents.OfType<PlayerLeveledUpEvent>().Count());
        }

        // ── CompleteChallenge ────────────────────────────────────────────────

        [Fact]
        public void CompleteChallenge_UnlocksEveryRewardKind()
        {
            var player = MakePlayer();
            var item = MakeItem(id: 10);

            player.CompleteChallenge(challengeId: 3, rewardItem: item, rewardItemModId: 5);

            Assert.Contains(player.Inventory.UnlockedItems, u => u.Item == item);
            Assert.Contains(5, player.Inventory.UnlockedMods);
        }

        [Fact]
        public void CompleteChallenge_RaisesChallengeCompletedEventWithRewardIds()
        {
            var player = MakePlayer();

            player.CompleteChallenge(challengeId: 3, rewardItem: MakeItem(id: 10), rewardItemModId: 5);

            var evt = player.DomainEvents.OfType<ChallengeCompletedEvent>().SingleOrDefault();
            Assert.NotNull(evt);
            Assert.Equal(player.Id, evt.PlayerId);
            Assert.Equal(3, evt.ChallengeId);
            Assert.Equal(10, evt.RewardItemId);
            Assert.Equal(5, evt.RewardItemModId);
        }

        [Fact]
        public void CompleteChallenge_NoRewards_UnlocksNothingButStillRaisesEvent()
        {
            var player = MakePlayer();

            player.CompleteChallenge(challengeId: 3, rewardItem: null, rewardItemModId: null);

            Assert.Empty(player.Inventory.UnlockedItems);
            Assert.Empty(player.Inventory.UnlockedMods);
            // The completion is still announced (e.g. a zone-gating challenge with no item reward) with
            // all reward ids null.
            var evt = player.DomainEvents.OfType<ChallengeCompletedEvent>().SingleOrDefault();
            Assert.NotNull(evt);
            Assert.Equal(3, evt.ChallengeId);
            Assert.Null(evt.RewardItemId);
            Assert.Null(evt.RewardItemModId);
        }

        [Fact]
        public void CompleteChallenge_NotifyFalse_UnlocksRewardsButSuppressesCompletedEvent()
        {
            var player = MakePlayer();
            var item = MakeItem(id: 10);

            // The offline batch completes challenges with the push suppressed: rewards are still unlocked (and
            // persisted via their own unlock events), but no ChallengeCompletedEvent is raised — the
            // welcome-back summary is the notification (spike #879 decision 7).
            player.CompleteChallenge(challengeId: 3, rewardItem: item, rewardItemModId: 5, notify: false);

            Assert.Contains(player.Inventory.UnlockedItems, u => u.Item == item);
            Assert.Contains(5, player.Inventory.UnlockedMods);
            Assert.Empty(player.DomainEvents.OfType<ChallengeCompletedEvent>());
            // The reward unlocks still raise their (persistence-only) events, so the completion persists.
            Assert.Single(player.DomainEvents.OfType<ItemUnlockedEvent>());
        }

        // ── TrySetSelectedSkills ─────────────────────────────────────────────

        [Fact]
        public void TrySetSelectedSkills_ValidSet_ReplacesEquippedSetInGivenOrder()
        {
            var player = MakePlayerWithUnlockedSkills(1, 2, 3);

            var result = player.TrySetSelectedSkills([3, 1, 2]);

            Assert.True(result);
            // The equipped set is the exact requested order (not re-sorted); the write-behind handler
            // persists each id's index as its Order so the mapper reproduces this order on reload.
            Assert.Equal([3, 1, 2], player.SelectedSkills.Select(s => s.Id));
        }

        [Fact]
        public void TrySetSelectedSkills_ValidSet_RaisesSelectedSkillsChangedEventWithOrderedIds()
        {
            var player = MakePlayerWithUnlockedSkills(1, 2, 3);

            player.TrySetSelectedSkills([3, 1]);

            var evt = player.DomainEvents.OfType<SelectedSkillsChangedEvent>().SingleOrDefault();
            Assert.NotNull(evt);
            Assert.Equal(player.Id, evt.PlayerId);
            Assert.Equal([3, 1], evt.OrderedSkillIds);
        }

        [Fact]
        public void TrySetSelectedSkills_OverCap_ReturnsFalseAndRaisesNoEvent()
        {
            // Five unlocked skills, selecting all of them — one over the cap of 4.
            var player = MakePlayerWithUnlockedSkills(1, 2, 3, 4, 5);
            player.ClearEvents();

            var result = player.TrySetSelectedSkills([1, 2, 3, 4, 5]);

            Assert.False(result);
            Assert.Empty(player.SelectedSkills);
            Assert.Empty(player.DomainEvents.OfType<SelectedSkillsChangedEvent>());
        }

        [Fact]
        public void TrySetSelectedSkills_DuplicateId_ReturnsFalseAndRaisesNoEvent()
        {
            var player = MakePlayerWithUnlockedSkills(1, 2, 3);
            player.ClearEvents();

            var result = player.TrySetSelectedSkills([1, 2, 1]);

            Assert.False(result);
            Assert.Empty(player.SelectedSkills);
            Assert.Empty(player.DomainEvents.OfType<SelectedSkillsChangedEvent>());
        }

        [Fact]
        public void TrySetSelectedSkills_DuplicateAtCapBoundary_ReturnsFalseAndRaisesNoEvent()
        {
            // A full-size loadout whose duplicate is the last pair — exercises the nested duplicate scan
            // across the whole list, not just an early-out at the front.
            var player = MakePlayerWithUnlockedSkills(1, 2, 3, 4);
            player.ClearEvents();

            var result = player.TrySetSelectedSkills([1, 2, 3, 1]);

            Assert.False(result);
            Assert.Empty(player.SelectedSkills);
            Assert.Empty(player.DomainEvents.OfType<SelectedSkillsChangedEvent>());
        }

        [Fact]
        public void TrySetSelectedSkills_NotUnlockedId_ReturnsFalseAndRaisesNoEvent()
        {
            var player = MakePlayerWithUnlockedSkills(1, 2, 3);
            player.ClearEvents();

            // 9 is not in the player's unlocked set, so the whole loadout is rejected.
            var result = player.TrySetSelectedSkills([1, 9]);

            Assert.False(result);
            Assert.Empty(player.SelectedSkills);
            Assert.Empty(player.DomainEvents.OfType<SelectedSkillsChangedEvent>());
        }

        [Fact]
        public void TrySetSelectedSkills_RejectedSet_LeavesExistingLoadoutUntouched()
        {
            var player = MakePlayerWithUnlockedSkills(1, 2, 3);
            player.TrySetSelectedSkills([1, 2]);
            player.ClearEvents();

            // A subsequent invalid request (duplicate) must not clobber the previously-equipped set.
            var result = player.TrySetSelectedSkills([3, 3]);

            Assert.False(result);
            Assert.Equal([1, 2], player.SelectedSkills.Select(s => s.Id));
            Assert.Empty(player.DomainEvents.OfType<SelectedSkillsChangedEvent>());
        }

        [Fact]
        public void TrySetSelectedSkills_ReorderOnly_UpdatesOrderAndRaisesEvent()
        {
            var player = MakePlayerWithUnlockedSkills(1, 2, 3);
            player.TrySetSelectedSkills([1, 2, 3]);
            player.ClearEvents();

            var result = player.TrySetSelectedSkills([3, 2, 1]);

            Assert.True(result);
            Assert.Equal([3, 2, 1], player.SelectedSkills.Select(s => s.Id));
            var evt = Assert.Single(player.DomainEvents.OfType<SelectedSkillsChangedEvent>());
            Assert.Equal([3, 2, 1], evt.OrderedSkillIds);
        }

        [Fact]
        public void TrySetSelectedSkills_DeselectToEmpty_ClearsLoadoutAndRaisesEventWithEmptyList()
        {
            var player = MakePlayerWithUnlockedSkills(1, 2, 3);
            player.TrySetSelectedSkills([1, 2]);
            player.ClearEvents();

            var result = player.TrySetSelectedSkills([]);

            Assert.True(result);
            Assert.Empty(player.SelectedSkills);
            var evt = Assert.Single(player.DomainEvents.OfType<SelectedSkillsChangedEvent>());
            Assert.Empty(evt.OrderedSkillIds);
        }

        [Fact]
        public void TrySetSelectedSkills_ExactlyAtCap_IsAccepted()
        {
            var player = MakePlayerWithUnlockedSkills(1, 2, 3, 4);
            player.ClearEvents();

            var result = player.TrySetSelectedSkills([1, 2, 3, 4]);

            Assert.True(result);
            Assert.Equal([1, 2, 3, 4], player.SelectedSkills.Select(s => s.Id));
        }

        // ── TryUnequipItem ───────────────────────────────────────────────────

        [Fact]
        public void TryUnequipItem_EquippedSlot_ClearsSlotAndRaisesItemUnequippedEvent()
        {
            var player = MakePlayer();
            player.UnlockItem(MakeItem(id: 10));
            player.TryEquipItem(10, EEquipmentSlot.AccessorySlot);
            player.ClearEvents();

            var result = player.TryUnequipItem(EEquipmentSlot.AccessorySlot);

            Assert.True(result);
            // The slot no longer holds the item.
            Assert.DoesNotContain(player.Inventory.EquipmentSlots, s => s.ItemId == 10);
            var evt = player.DomainEvents.OfType<ItemUnequippedEvent>().SingleOrDefault();
            Assert.NotNull(evt);
            Assert.Equal(player.Id, evt.PlayerId);
            Assert.Equal(10, evt.ItemId);
        }

        [Fact]
        public void TryUnequipItem_EmptySlot_ReturnsFalseAndRaisesNoEvent()
        {
            var player = MakePlayer();
            player.ClearEvents();

            // Nothing is equipped in the slot, so there is nothing to unequip.
            var result = player.TryUnequipItem(EEquipmentSlot.AccessorySlot);

            Assert.False(result);
            Assert.Empty(player.DomainEvents.OfType<ItemUnequippedEvent>());
        }

        // ── TryRemoveMod ─────────────────────────────────────────────────────

        [Fact]
        public void TryRemoveMod_AppliedMod_RemovesModAndRaisesModRemovedEvent()
        {
            var player = MakePlayer();
            player.UnlockItem(MakeItemWithPrefixSlot(id: 10));
            player.UnlockMod(5);
            player.TryApplyMod(10, 5, 0, MakeMod(5, EItemModType.Prefix));
            player.ClearEvents();

            var result = player.TryRemoveMod(10, 0);

            Assert.True(result);
            Assert.Empty(player.Inventory.UnlockedItems.Single().AppliedMods);
            var evt = player.DomainEvents.OfType<ModRemovedEvent>().SingleOrDefault();
            Assert.NotNull(evt);
            Assert.Equal(player.Id, evt.PlayerId);
            Assert.Equal(10, evt.ItemId);
            Assert.Equal(0, evt.ItemModSlotId);
        }

        [Fact]
        public void TryRemoveMod_NoModInSlot_ReturnsFalseAndRaisesNoEvent()
        {
            var player = MakePlayer();
            player.UnlockItem(MakeItemWithPrefixSlot(id: 10));
            player.ClearEvents();

            // The item is unlocked but the slot has no applied mod to remove.
            var result = player.TryRemoveMod(10, 0);

            Assert.False(result);
            Assert.Empty(player.DomainEvents.OfType<ModRemovedEvent>());
        }

        [Fact]
        public void TryRemoveMod_ItemNotUnlocked_ReturnsFalseAndRaisesNoEvent()
        {
            var player = MakePlayer();
            player.ClearEvents();

            var result = player.TryRemoveMod(999, 0);

            Assert.False(result);
            Assert.Empty(player.DomainEvents.OfType<ModRemovedEvent>());
        }

        // ── TrySetFavorite ───────────────────────────────────────────────────

        [Fact]
        public void TrySetFavorite_UnlockedItem_SetsFlagAndRaisesItemFavoriteChangedEvent()
        {
            var player = MakePlayer();
            player.UnlockItem(MakeItem(id: 10));
            player.ClearEvents();

            var result = player.TrySetFavorite(10, true);

            Assert.True(result);
            Assert.True(player.Inventory.UnlockedItems.Single().Favorite);
            var evt = player.DomainEvents.OfType<ItemFavoriteChangedEvent>().SingleOrDefault();
            Assert.NotNull(evt);
            Assert.Equal(player.Id, evt.PlayerId);
            Assert.Equal(10, evt.ItemId);
            Assert.True(evt.Favorite);
        }

        [Fact]
        public void TrySetFavorite_CanUnfavorite_RaisesEventWithFalse()
        {
            var player = MakePlayer();
            player.UnlockItem(MakeItem(id: 10));
            player.TrySetFavorite(10, true);
            player.ClearEvents();

            var result = player.TrySetFavorite(10, false);

            Assert.True(result);
            Assert.False(player.Inventory.UnlockedItems.Single().Favorite);
            var evt = player.DomainEvents.OfType<ItemFavoriteChangedEvent>().SingleOrDefault();
            Assert.NotNull(evt);
            Assert.False(evt.Favorite);
        }

        [Fact]
        public void TrySetFavorite_ItemNotUnlocked_ReturnsFalseAndRaisesNoEvent()
        {
            var player = MakePlayer();

            var result = player.TrySetFavorite(999, true);

            Assert.False(result);
            Assert.Empty(player.DomainEvents.OfType<ItemFavoriteChangedEvent>());
        }

        // ── UpdateLogPreference ──────────────────────────────────────────────

        [Fact]
        public void UpdateLogPreference_NewType_AddsPreference()
        {
            var player = MakePlayer();

            player.UpdateLogPreference(ELogType.Damage, false);

            var pref = player.LogPreferences.SingleOrDefault(p => p.LogType == ELogType.Damage);
            Assert.NotNull(pref);
            Assert.False(pref.Enabled);
        }

        [Fact]
        public void UpdateLogPreference_ExistingType_UpdatesInPlace()
        {
            var player = MakePlayer();
            player.LogPreferences.Add(new LogPreference { LogType = ELogType.Damage, Enabled = true });

            player.UpdateLogPreference(ELogType.Damage, false);

            var pref = Assert.Single(player.LogPreferences);
            Assert.Equal(ELogType.Damage, pref.LogType);
            Assert.False(pref.Enabled);
        }

        [Fact]
        public void UpdateLogPreference_RaisesLogPreferenceChangedEvent()
        {
            var player = MakePlayer();

            player.UpdateLogPreference(ELogType.Debug, true);

            var evt = player.DomainEvents.OfType<LogPreferenceChangedEvent>().SingleOrDefault();
            Assert.NotNull(evt);
            Assert.Equal(player.Id, evt.PlayerId);
            Assert.Equal(ELogType.Debug, evt.LogType);
            Assert.True(evt.Enabled);
        }

        // ── RecordBattleCompleted ────────────────────────────────────────────

        [Fact]
        public void RecordBattleCompleted_RaisesBattleCompletedEvent()
        {
            var player = MakePlayer();
            var enemy = MakeEnemy(id: 5);
            var stats = new BattleStats { PlayerDamageDealt = 42.0 };
            var result = new BattleResult(Victory: true, PlayerDied: false, TotalMs: 3200, Stats: stats);

            player.RecordBattleCompleted(enemy, result, isBossBattle: true, zoneId: 7, timestamp: DateTime.UtcNow);

            var evt = player.DomainEvents.OfType<BattleCompletedEvent>().SingleOrDefault();
            Assert.NotNull(evt);
            Assert.Equal(player, evt.Player);
            Assert.Equal(enemy, evt.Enemy);
            Assert.True(evt.Victory);
            Assert.False(evt.PlayerDied);
            Assert.Equal(3200, evt.TotalMs);
            Assert.Equal(stats, evt.Stats);
            Assert.True(evt.IsBossBattle);
            Assert.Equal(7, evt.ZoneId);
        }

        [Fact]
        public void RecordBattleCompleted_StampsLastActivityAndRaisesCoreUpdated()
        {
            var player = MakePlayer();
            var enemy = MakeEnemy(id: 5);
            var result = new BattleResult(Victory: false, PlayerDied: true, TotalMs: 1000, Stats: new BattleStats());
            var timestamp = new DateTime(2026, 6, 20, 12, 0, 0, DateTimeKind.Utc);

            player.RecordBattleCompleted(enemy, result, isBossBattle: false, zoneId: 3, timestamp: timestamp);

            Assert.Equal(timestamp, player.LastActivity);
            var coreUpdated = player.DomainEvents.OfType<PlayerCoreUpdatedEvent>().SingleOrDefault();
            Assert.NotNull(coreUpdated);
            Assert.Equal(timestamp, coreUpdated.LastActivity);
        }

        // ── StampActivity ────────────────────────────────────────────────────

        [Fact]
        public void StampActivity_SetsLastActivityAndRaisesCoreUpdatedCarryingIt()
        {
            var player = MakePlayer();
            var timestamp = new DateTime(2026, 6, 20, 15, 30, 0, DateTimeKind.Utc);

            player.StampActivity(timestamp);

            Assert.Equal(timestamp, player.LastActivity);
            var evt = Assert.IsType<PlayerCoreUpdatedEvent>(Assert.Single(player.DomainEvents));
            Assert.Equal(timestamp, evt.LastActivity);
        }

        // ── SetAutoChallengeBoss ─────────────────────────────────────────────

        [Fact]
        public void SetAutoChallengeBoss_Enabled_EntersBossModeAndRaisesCoreUpdatedCarryingIt()
        {
            var player = MakePlayer();

            player.SetAutoChallengeBoss(true);

            Assert.True(player.AutoChallengeBoss);
            var evt = Assert.IsType<PlayerCoreUpdatedEvent>(Assert.Single(player.DomainEvents));
            Assert.True(evt.AutoChallengeBoss);
        }

        [Fact]
        public void SetAutoChallengeBoss_Disabled_ReturnsToIdleAndRaisesCoreUpdatedCarryingIt()
        {
            var player = MakePlayer();
            player.SetAutoChallengeBoss(true);
            player.ClearEvents();

            player.SetAutoChallengeBoss(false);

            Assert.False(player.AutoChallengeBoss);
            var evt = Assert.IsType<PlayerCoreUpdatedEvent>(Assert.Single(player.DomainEvents));
            Assert.False(evt.AutoChallengeBoss);
        }

        // ── RecordBattleCompleted — boss-mode backstop ───────────────────────

        [Fact]
        public void RecordBattleCompleted_BossLoss_ResetsModeToIdle()
        {
            var player = MakePlayer();
            player.SetAutoChallengeBoss(true);
            var result = new BattleResult(Victory: false, PlayerDied: true, TotalMs: 1000, Stats: new BattleStats());

            player.RecordBattleCompleted(MakeEnemy(), result, isBossBattle: true, zoneId: 7, timestamp: DateTime.UtcNow);

            // A recorded boss loss drops the persisted loop back to idle (mirrors the online auto-fight-off).
            Assert.False(player.AutoChallengeBoss);
        }

        [Fact]
        public void RecordBattleCompleted_BossDraw_ResetsModeToIdle()
        {
            var player = MakePlayer();
            player.SetAutoChallengeBoss(true);
            // A draw is neither side dying within the cap: not a victory, so it resolves the same as a loss.
            var result = new BattleResult(Victory: false, PlayerDied: false, TotalMs: 120000, Stats: new BattleStats());

            player.RecordBattleCompleted(MakeEnemy(), result, isBossBattle: true, zoneId: 7, timestamp: DateTime.UtcNow);

            Assert.False(player.AutoChallengeBoss);
        }

        [Fact]
        public void RecordBattleCompleted_BossVictory_KeepsBossMode()
        {
            var player = MakePlayer();
            player.SetAutoChallengeBoss(true);
            var result = new BattleResult(Victory: true, PlayerDied: false, TotalMs: 3000, Stats: new BattleStats());

            player.RecordBattleCompleted(MakeEnemy(), result, isBossBattle: true, zoneId: 7, timestamp: DateTime.UtcNow);

            // A boss win keeps farming the boss — the persisted mode is unchanged.
            Assert.True(player.AutoChallengeBoss);
        }

        [Fact]
        public void RecordBattleCompleted_IdleLoss_DoesNotTouchBossMode()
        {
            var player = MakePlayer();
            player.SetAutoChallengeBoss(true);
            var result = new BattleResult(Victory: false, PlayerDied: true, TotalMs: 1000, Stats: new BattleStats());

            // An idle (non-boss) loss never affects the persisted boss mode.
            player.RecordBattleCompleted(MakeEnemy(), result, isBossBattle: false, zoneId: 3, timestamp: DateTime.UtcNow);

            Assert.True(player.AutoChallengeBoss);
        }

        [Fact]
        public void RecordBattleCompleted_BossLoss_CarriesClearedModeInASingleCoreUpdatedEvent()
        {
            var player = MakePlayer();
            player.SetAutoChallengeBoss(true);
            player.ClearEvents();
            var result = new BattleResult(Victory: false, PlayerDied: true, TotalMs: 1000, Stats: new BattleStats());

            player.RecordBattleCompleted(MakeEnemy(), result, isBossBattle: true, zoneId: 7, timestamp: DateTime.UtcNow);

            // The mode reset rides the single core-updated event StampActivity raises, not a redundant second one.
            var coreUpdated = Assert.Single(player.DomainEvents.OfType<PlayerCoreUpdatedEvent>());
            Assert.False(coreUpdated.AutoChallengeBoss);
        }

        // ── ClearEvents ──────────────────────────────────────────────────────

        [Fact]
        public void ClearEvents_RemovesAllCollectedEvents()
        {
            var player = MakePlayer(level: 1, exp: 0);
            player.GrantExp(101);               // produces one event
            Assert.True(player.DomainEvents.Count > 0);

            player.ClearEvents();

            Assert.Empty(player.DomainEvents);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static Player MakePlayer(int level = 1, int exp = 0) =>
            new PlayerBuilder().WithLevel(level).WithExp(exp).Build();

        /// <summary>Builds a player whose unlocked set contains a skill for each given id (none equipped).</summary>
        private static Player MakePlayerWithUnlockedSkills(params int[] skillIds)
        {
            var player = MakePlayer();
            player.Skills = skillIds.Select(MakeSkill).ToList();
            return player;
        }

        private static Enemy MakeEnemy(int id = 1) => new()
        {
            Id = id,
            Name = "Test Enemy",
            Level = 1,
            IsBoss = false,
            AttributeDistributions = [],
            AvailableSkills = [],
        };
        private static Item MakeItem(int id, EItemCategory category = EItemCategory.Accessory, ERarity rarity = ERarity.Common,
            List<AttributeModifier>? attributes = null, List<ItemModSlot>? modSlots = null) => new()
            {
                Id = id,
                Name = $"Item {id}",
                Description = string.Empty,
                Category = category,
                Rarity = rarity,
                Attributes = attributes ?? [],
                ModSlots = modSlots ?? [],
            };

        /// <summary>An accessory carrying a single Prefix mod slot (Id 0), for the apply/remove-mod paths.</summary>
        private static Item MakeItemWithPrefixSlot(int id) => MakeItem(id, modSlots:
        [
            new ItemModSlot { Id = 0, Type = EItemModType.Prefix },
        ]);

        private static ItemMod MakeMod(int id, EItemModType type) => new()
        {
            Id = id,
            Name = $"Mod {id}",
            Description = string.Empty,
            Type = type,
            Rarity = ERarity.Common,
            Attributes = [],
        };

        private static Skill MakeSkill(int id) => new()
        {
            Id = id,
            Name = $"Skill {id}",
            BaseDamage = 10,
            Description = string.Empty,
            Rarity = ERarity.Common,
            CooldownMs = 1000,
            DamageMultipliers = [],
            Effects = [],
        };
    }
}
