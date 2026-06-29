using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Battle;
using Game.Core.Classes;
using Game.Core.Items;
using Game.Core.Players;
using Game.Core.Players.Inventories;
using Game.Core.Proficiencies;
using Game.Core.Skills;
using Game.Core.TestInfrastructure.Builders;
using Xunit;
using CoreClass = Game.Core.Classes.Class;

namespace Game.Core.Tests.Battle
{
    public class BattleSnapshotTests
    {
        // ── FromPlayer ───────────────────────────────────────────────────────

        [Fact]
        public void FromPlayer_CapturesLevelStatsEquipmentAndSkills()
        {
            var item = MakeItem(1, attributes: [MakeModifier(EAttribute.Strength, 5)],
                modSlots: [new ItemModSlot { Id = 0, Type = EItemModType.Prefix }]);
            var mod = MakeMod(10, EItemModType.Prefix, [MakeModifier(EAttribute.Dexterity, 7)]);

            var player = MakePlayer(
                level: 9,
                allocations: [Alloc(EAttribute.Strength, 4)],
                selectedSkills: [MakeSkill(2), MakeSkill(3)]);
            EquipWithMod(player, item, mod, modSlotIndex: 0);

            var snapshot = BattleSnapshot.FromPlayer(player);

            Assert.Equal(9, snapshot.Level);
            // The snapshot captures an independent copy of the allocations, not the live list/elements.
            Assert.NotSame(player.StatPoints.StatAllocations, snapshot.StatAllocations);
            var allocation = Assert.Single(snapshot.StatAllocations);
            Assert.NotSame(player.StatPoints.StatAllocations[0], allocation);
            Assert.Equal(EAttribute.Strength, allocation.Attribute);
            Assert.Equal(4, allocation.Amount);
            Assert.Equal([2, 3], snapshot.SkillIds);
            var equipped = Assert.Single(snapshot.EquippedItems);
            Assert.Equal(1, equipped.ItemId);
            Assert.Equal([10], equipped.AppliedModIds);
        }

        [Fact]
        public void FromPlayer_StatAllocations_AreImmuneToLaterLiveMutation()
        {
            var player = MakePlayer(allocations: [Alloc(EAttribute.Strength, 4)]);

            var snapshot = BattleSnapshot.FromPlayer(player);

            // Reallocating stats mutates StatAllocation.Amount in place on the live player; the snapshot
            // is an immutable capture taken at battle start and must not be affected.
            player.StatPoints.StatAllocations[0].Amount = 99;

            var captured = Assert.Single(snapshot.StatAllocations);
            Assert.Equal(4, captured.Amount);
        }

        [Fact]
        public void FromPlayer_NoEquipment_CapturesEmptyEquippedItems()
        {
            var player = MakePlayer(selectedSkills: [MakeSkill(2)]);

            var snapshot = BattleSnapshot.FromPlayer(player);

            Assert.Empty(snapshot.EquippedItems);
        }

        [Fact]
        public void FromPlayer_EquippedItemWithoutMods_CapturesEmptyAppliedModIds()
        {
            var player = MakePlayer();
            Equip(player, MakeItem(1, attributes: [MakeModifier(EAttribute.Strength, 5)]));

            var snapshot = BattleSnapshot.FromPlayer(player);

            var equipped = Assert.Single(snapshot.EquippedItems);
            Assert.Equal(1, equipped.ItemId);
            Assert.Empty(equipped.AppliedModIds);
        }

        [Fact]
        public void FromPlayer_EquippedItemMissingFromUnlockedItems_ThrowsNamingItem()
        {
            var player = MakePlayer();
            Equip(player, MakeItem(7, attributes: [MakeModifier(EAttribute.Strength, 5)]));

            // Corrupt the inventory invariant: an item stays equipped but is no longer unlocked. The
            // snapshot must fail loudly rather than silently capturing the item without its mods.
            player.Inventory.UnlockedItems = [];

            var ex = Assert.Throws<InvalidOperationException>(() => BattleSnapshot.FromPlayer(player));
            Assert.Contains("Equipped item 7", ex.Message);
        }

        // ── ToBattler ────────────────────────────────────────────────────────

        [Fact]
        public void ToBattler_ComposesStatItemAndModModifiers()
        {
            var snapshot = new BattleSnapshot
            {
                Level = 7,
                StatAllocations = [Alloc(EAttribute.Strength, 4)],
                EquippedItems = [new EquippedItemSnapshot { ItemId = 1, AppliedModIds = [10] }],
                SkillIds = [2],
            };

            var battler = snapshot.ToBattler(
                ItemResolver(MakeItem(1, attributes: [MakeModifier(EAttribute.Strength, 5)])),
                ModResolver(MakeMod(10, EItemModType.Prefix, [MakeModifier(EAttribute.Strength, 3)])),
                SkillResolver(MakeSkill(2)));

            Assert.Equal(7, battler.Level);
            Assert.Equal([2], battler.Skills.Select(s => s.Skill.Id));
            // Strength = 4 (allocation) + 5 (item) + 3 (mod) = 12
            Assert.Equal(12, battler.GetAttributeValue(EAttribute.Strength));
        }

        [Fact]
        public void ToBattler_NoEquipment_AppliesOnlyStatModifiers()
        {
            var snapshot = new BattleSnapshot
            {
                Level = 3,
                StatAllocations = [Alloc(EAttribute.Strength, 8)],
                EquippedItems = [],
                SkillIds = [],
            };

            var battler = snapshot.ToBattler(ThrowItem, ThrowMod, ThrowSkill);

            Assert.Equal(8, battler.GetAttributeValue(EAttribute.Strength));
        }

        [Fact]
        public void ToBattler_EmptySkillList_ProducesNoSkills()
        {
            var snapshot = new BattleSnapshot
            {
                Level = 1,
                StatAllocations = [],
                EquippedItems = [],
                SkillIds = [],
            };

            var battler = snapshot.ToBattler(ThrowItem, ThrowMod, ThrowSkill);

            Assert.Empty(battler.Skills);
        }

        [Fact]
        public void ToBattler_StackedModsOnOneItem_IncludesEveryModContribution()
        {
            var snapshot = new BattleSnapshot
            {
                Level = 1,
                StatAllocations = [],
                EquippedItems = [new EquippedItemSnapshot { ItemId = 1, AppliedModIds = [10, 11] }],
                SkillIds = [],
            };

            var battler = snapshot.ToBattler(
                ItemResolver(MakeItem(1, attributes: [MakeModifier(EAttribute.Strength, 2)])),
                ModResolver(
                    MakeMod(10, EItemModType.Prefix, [MakeModifier(EAttribute.Strength, 3)]),
                    MakeMod(11, EItemModType.Suffix, [MakeModifier(EAttribute.Strength, 4)])),
                ThrowSkill);

            // Strength = 2 (item) + 3 (mod 10) + 4 (mod 11) = 9
            Assert.Equal(9, battler.GetAttributeValue(EAttribute.Strength));
        }

        // ── Proficiency bonuses ──────────────────────────────────────────────

        [Fact]
        public void FromPlayer_CapturesSuppliedProficiencyLevels()
        {
            var player = MakePlayer();

            var snapshot = BattleSnapshot.FromPlayer(player,
                [new ProficiencyLevelSnapshot { ProficiencyId = 3, Level = 2 }]);

            var captured = Assert.Single(snapshot.ProficiencyLevels);
            Assert.Equal(3, captured.ProficiencyId);
            Assert.Equal(2, captured.Level);
        }

        [Fact]
        public void FromPlayer_NoProficiencyLevelsSupplied_CapturesEmpty()
        {
            var snapshot = BattleSnapshot.FromPlayer(MakePlayer());

            Assert.Empty(snapshot.ProficiencyLevels);
        }

        [Fact]
        public void ToBattler_AppliesCapturedProficiencyLevelBonuses()
        {
            var snapshot = new BattleSnapshot
            {
                Level = 1,
                StatAllocations = [Alloc(EAttribute.Strength, 4)],
                EquippedItems = [],
                SkillIds = [],
                ProficiencyLevels = [new ProficiencyLevelSnapshot { ProficiencyId = 5, Level = 2 }],
            };

            // A proficiency granting +3 Strength at level 1 and +6 at level 2; the player is level 2, so both apply.
            var battler = snapshot.ToBattler(ThrowItem, ThrowMod, ThrowSkill,
                ProficiencyResolver(MakeProficiency(5,
                    (1, [ProfMod(EAttribute.Strength, 3)]),
                    (2, [ProfMod(EAttribute.Strength, 6)]))));

            // Strength = 4 (allocation) + 3 + 6 (proficiency) = 13.
            Assert.Equal(13, battler.GetAttributeValue(EAttribute.Strength));
        }

        [Fact]
        public void GetModifiers_ProficiencyLevelsCapturedButNoResolver_Throws()
        {
            var snapshot = new BattleSnapshot
            {
                Level = 1,
                StatAllocations = [],
                EquippedItems = [],
                SkillIds = [],
                ProficiencyLevels = [new ProficiencyLevelSnapshot { ProficiencyId = 5, Level = 1 }],
            };

            Assert.Throws<InvalidOperationException>(() => snapshot.GetModifiers(ThrowItem, ThrowMod).ToList());
        }

        // ── Class locked base ────────────────────────────────────────────────

        [Fact]
        public void FromPlayer_CapturesClassId()
        {
            var player = new PlayerBuilder().WithClassId(4).Build();

            var snapshot = BattleSnapshot.FromPlayer(player);

            Assert.Equal(4, snapshot.ClassId);
        }

        [Fact]
        public void ToBattler_AppliesClassLockedBaseDistribution()
        {
            // The class's locked base scales with the captured level (BaseAmount + AmountPerLevel × level), the
            // same math an enemy's distribution uses, and composes additively with the free-pool allocation.
            var snapshot = new BattleSnapshot
            {
                Level = 5,
                ClassId = 2,
                StatAllocations = [Alloc(EAttribute.Strength, 4)],
                EquippedItems = [],
                SkillIds = [],
            };

            var battler = snapshot.ToBattler(ThrowItem, ThrowMod, ThrowSkill,
                resolveClass: ClassResolver(MakeClass(2,
                    Distribution(EAttribute.Strength, 10m, amountPerLevel: 2m),
                    Distribution(EAttribute.Endurance, 3m))));

            // Strength = 4 (free pool) + (10 + 2 × 5) (locked base) = 24; Endurance = 0 + (3 + 0 × 5) = 3.
            Assert.Equal(24, battler.GetAttributeValue(EAttribute.Strength));
            Assert.Equal(3, battler.GetAttributeValue(EAttribute.Endurance));
        }

        [Fact]
        public void ToBattler_ComposesFlatClassSignaturePassive()
        {
            var snapshot = new BattleSnapshot
            {
                Level = 1,
                ClassId = 3,
                StatAllocations = [Alloc(EAttribute.Strength, 6)],
                EquippedItems = [],
                SkillIds = [],
            };

            var passive = new ClassSignaturePassive
            {
                Attribute = EAttribute.Strength,
                Amount = 4m,
                ScalingAttribute = null,
                ScalingAmount = 0m,
                ModifierType = EModifierType.Additive,
            };
            var battler = snapshot.ToBattler(ThrowItem, ThrowMod, ThrowSkill,
                resolveClass: ClassResolver(MakeClassWithPassive(3, passive)));

            // Strength = 6 (free pool) + 4 (flat signature passive) = 10.
            Assert.Equal(10, battler.GetAttributeValue(EAttribute.Strength));
        }

        [Fact]
        public void ToBattler_ComposesAttributeScaledClassSignaturePassive_OffAssembledValue()
        {
            // The passive scales off the fully-assembled Endurance (free pool + locked base), landing on Toughness
            // which also carries the static Endurance-derived term — pinning that the passive reads the same
            // resolved Endurance and accumulates after the statics.
            var snapshot = new BattleSnapshot
            {
                Level = 2,
                ClassId = 3,
                StatAllocations = [Alloc(EAttribute.Endurance, 5)],
                EquippedItems = [],
                SkillIds = [],
            };

            var passive = new ClassSignaturePassive
            {
                Attribute = EAttribute.Toughness,
                Amount = 2m,
                ScalingAttribute = EAttribute.Endurance,
                ScalingAmount = 0.5m,
                ModifierType = EModifierType.Additive,
            };
            var battler = snapshot.ToBattler(ThrowItem, ThrowMod, ThrowSkill,
                resolveClass: ClassResolver(MakeClassWithPassive(3, passive, Distribution(EAttribute.Endurance, 4m, amountPerLevel: 3m))));

            // Endurance = 5 (free pool) + (4 + 3 × 2) locked base = 15.
            // Toughness (static 2·Endurance = 30) + passive (2 + 0.5 × 15 = 9.5) = 39.5.
            Assert.Equal(15, battler.GetAttributeValue(EAttribute.Endurance));
            Assert.Equal(39.5, battler.GetAttributeValue(EAttribute.Toughness));
        }

        [Fact]
        public void GetModifiersWithSignaturePassive_IncludesPassive_SoPowerCountsItLikeTheLockedBase()
        {
            var snapshot = new BattleSnapshot
            {
                Level = 1,
                ClassId = 3,
                StatAllocations = [Alloc(EAttribute.Strength, 6)],
                EquippedItems = [],
                SkillIds = [],
            };

            var passive = new ClassSignaturePassive
            {
                Attribute = EAttribute.Strength,
                Amount = 4m,
                ScalingAttribute = null,
                ScalingAmount = 0m,
                ModifierType = EModifierType.Additive,
            };
            var modifiers = snapshot
                .GetModifiersWithSignaturePassive(ThrowItem, ThrowMod, resolveClass: ClassResolver(MakeClassWithPassive(3, passive)))
                .ToList();

            // The flat-core passive (a class-identity bonus) is present, so DefeatRewards' core-additive power
            // sum counts it just like the locked base — not silently dropped from the reward heuristic.
            var classModifier = Assert.Single(modifiers, m => m.Source == EAttributeModifierSource.Class);
            Assert.Equal(EAttribute.Strength, classModifier.Attribute);
            Assert.Equal(4d, classModifier.Amount);
        }

        [Fact]
        public void GetModifiersWithSignaturePassive_NoClassCaptured_ReturnsBaseModifiersOnly()
        {
            var snapshot = new BattleSnapshot
            {
                Level = 1,
                StatAllocations = [Alloc(EAttribute.Strength, 6)],
                EquippedItems = [],
                SkillIds = [],
            };

            var modifiers = snapshot.GetModifiersWithSignaturePassive(ThrowItem, ThrowMod).ToList();

            Assert.DoesNotContain(modifiers, m => m.Source == EAttributeModifierSource.Class);
        }

        [Fact]
        public void GetModifiers_ClassCapturedButNoResolver_Throws()
        {
            var snapshot = new BattleSnapshot
            {
                Level = 1,
                ClassId = 0,
                StatAllocations = [],
                EquippedItems = [],
                SkillIds = [],
            };

            Assert.Throws<InvalidOperationException>(() => snapshot.GetModifiers(ThrowItem, ThrowMod).ToList());
        }

        [Fact]
        public void GetModifiers_NoClassCaptured_OmitsLockedBase()
        {
            // A hand-built snapshot with no ClassId resolves no locked base and needs no class resolver.
            var snapshot = new BattleSnapshot
            {
                Level = 9,
                StatAllocations = [Alloc(EAttribute.Strength, 7)],
                EquippedItems = [],
                SkillIds = [],
            };

            var modifier = Assert.Single(snapshot.GetModifiers(ThrowItem, ThrowMod));
            Assert.Equal(EAttribute.Strength, modifier.Attribute);
            Assert.Equal(7, modifier.Amount);
            Assert.Equal(EAttributeModifierSource.PlayerStatPoints, modifier.Source);
        }

        // ── Item-granted skills (append + order + dedupe) ────────────────────

        [Fact]
        public void ToBattler_AppendsEquippedItemGrantedSkillAfterSelected()
        {
            var snapshot = new BattleSnapshot
            {
                Level = 1,
                StatAllocations = [],
                EquippedItems = [new EquippedItemSnapshot { ItemId = 1, AppliedModIds = [] }],
                SkillIds = [2],
            };

            var battler = snapshot.ToBattler(
                ItemResolver(MakeItem(1, grantedSkillId: 3)),
                ThrowMod,
                SkillResolver(MakeSkill(2), MakeSkill(3)));

            // Selected skill first, then the item's granted skill.
            Assert.Equal([2, 3], battler.Skills.Select(s => s.Skill.Id));
        }

        [Fact]
        public void ToBattler_ItemWithoutGrant_AddsOnlySelectedSkills()
        {
            var snapshot = new BattleSnapshot
            {
                Level = 1,
                StatAllocations = [],
                EquippedItems = [new EquippedItemSnapshot { ItemId = 1, AppliedModIds = [] }],
                SkillIds = [2],
            };

            var battler = snapshot.ToBattler(
                ItemResolver(MakeItem(1, grantedSkillId: null)),
                ThrowMod,
                SkillResolver(MakeSkill(2)));

            Assert.Equal([2], battler.Skills.Select(s => s.Skill.Id));
        }

        [Fact]
        public void ToBattler_GrantedSkillsFollowEquippedItemOrder()
        {
            // EquippedItems is captured in EEquipmentSlot order by FromPlayer, so the granted skills follow
            // that order: the first item's grant before the second's.
            var snapshot = new BattleSnapshot
            {
                Level = 1,
                StatAllocations = [],
                EquippedItems =
                [
                    new EquippedItemSnapshot { ItemId = 1, AppliedModIds = [] },
                    new EquippedItemSnapshot { ItemId = 2, AppliedModIds = [] },
                ],
                SkillIds = [5],
            };

            var battler = snapshot.ToBattler(
                ItemResolver(MakeItem(1, grantedSkillId: 6), MakeItem(2, grantedSkillId: 7)),
                ThrowMod,
                SkillResolver(MakeSkill(5), MakeSkill(6), MakeSkill(7)));

            Assert.Equal([5, 6, 7], battler.Skills.Select(s => s.Skill.Id));
        }

        [Fact]
        public void ToBattler_GrantedSkillDuplicatingSelected_AppearsOnce()
        {
            var snapshot = new BattleSnapshot
            {
                Level = 1,
                StatAllocations = [],
                EquippedItems = [new EquippedItemSnapshot { ItemId = 1, AppliedModIds = [] }],
                SkillIds = [3],
            };

            var battler = snapshot.ToBattler(
                ItemResolver(MakeItem(1, grantedSkillId: 3)),
                ThrowMod,
                SkillResolver(MakeSkill(3)));

            // The grant duplicates the selected skill, so it is fielded once (first occurrence wins).
            Assert.Equal([3], battler.Skills.Select(s => s.Skill.Id));
        }

        [Fact]
        public void ToBattler_DuplicateSelectedSkill_AppearsOnce()
        {
            var snapshot = new BattleSnapshot
            {
                Level = 1,
                StatAllocations = [],
                EquippedItems = [],
                SkillIds = [3, 3],
            };

            var battler = snapshot.ToBattler(
                ThrowItem,
                ThrowMod,
                SkillResolver(MakeSkill(3)));

            // The same skill selected twice is de-duplicated within the selected loadout (first wins).
            Assert.Equal([3], battler.Skills.Select(s => s.Skill.Id));
        }

        [Fact]
        public void ToBattler_TwoItemsGrantingSameSkill_AppearsOnce()
        {
            var snapshot = new BattleSnapshot
            {
                Level = 1,
                StatAllocations = [],
                EquippedItems =
                [
                    new EquippedItemSnapshot { ItemId = 1, AppliedModIds = [] },
                    new EquippedItemSnapshot { ItemId = 2, AppliedModIds = [] },
                ],
                SkillIds = [],
            };

            var battler = snapshot.ToBattler(
                ItemResolver(MakeItem(1, grantedSkillId: 9), MakeItem(2, grantedSkillId: 9)),
                ThrowMod,
                SkillResolver(MakeSkill(9)));

            Assert.Equal([9], battler.Skills.Select(s => s.Skill.Id));
        }

        // ── Round-trip parity with the live Player battler ───────────────────
        // The snapshot path must reconstruct exactly the same attributes the live Player aggregate
        // composes — this is the frontend/backend battle-parity (anti-cheat) guarantee.

        [Fact]
        public void RoundTrip_FullLoadout_MatchesLivePlayerBattler()
        {
            var item = MakeItem(1,
                attributes: [MakeModifier(EAttribute.Strength, 5), MakeModifier(EAttribute.Endurance, 2)],
                modSlots: [new ItemModSlot { Id = 0, Type = EItemModType.Prefix }]);
            var mod = MakeMod(10, EItemModType.Prefix, [MakeModifier(EAttribute.Dexterity, 7)]);
            var skillA = MakeSkill(2);
            var skillB = MakeSkill(3);

            var player = MakePlayer(
                level: 12,
                allocations: [Alloc(EAttribute.Strength, 4), Alloc(EAttribute.Agility, 6)],
                selectedSkills: [skillA, skillB]);
            EquipWithMod(player, item, mod, modSlotIndex: 0);

            AssertBattlerParity(player, ItemResolver(item), ModResolver(mod), SkillResolver(skillA, skillB));
        }

        [Fact]
        public void RoundTrip_NoEquipment_MatchesLivePlayerBattler()
        {
            var skill = MakeSkill(2);
            var player = MakePlayer(
                level: 5,
                allocations: [Alloc(EAttribute.Endurance, 10)],
                selectedSkills: [skill]);

            AssertBattlerParity(player, ThrowItem, ThrowMod, SkillResolver(skill));
        }

        [Fact]
        public void RoundTrip_StackedMods_MatchesLivePlayerBattler()
        {
            var item = MakeItem(1,
                attributes: [MakeModifier(EAttribute.Strength, 5)],
                modSlots:
                [
                    new ItemModSlot { Id = 0, Type = EItemModType.Prefix },
                    new ItemModSlot { Id = 1, Type = EItemModType.Suffix },
                ]);
            var prefix = MakeMod(10, EItemModType.Prefix, [MakeModifier(EAttribute.Strength, 3)]);
            var suffix = MakeMod(11, EItemModType.Suffix, [MakeModifier(EAttribute.Dexterity, 4)]);

            var player = MakePlayer(level: 8, allocations: [Alloc(EAttribute.Agility, 2)]);
            Equip(player, item);
            player.UnlockMod(prefix.Id);
            player.UnlockMod(suffix.Id);
            player.TryApplyMod(item.Id, prefix.Id, 0, prefix);
            player.TryApplyMod(item.Id, suffix.Id, 1, suffix);

            AssertBattlerParity(player, ItemResolver(item), ModResolver(prefix, suffix), ThrowSkill);
        }

        [Fact]
        public void RoundTrip_ItemGrantedSkills_MatchSlotOrderedLivePlayerBattler()
        {
            // Two items granting skills, equipped in reverse slot order (accessory first, then helm), plus a
            // selected skill. Both the live and snapshot paths must field: selected, then the grants in
            // EEquipmentSlot order (helm before accessory) — proving the slot-ordered assembly round-trips.
            var helm = MakeItem(1, category: EItemCategory.Helm, grantedSkillId: 6);
            var accessory = MakeItem(2, category: EItemCategory.Accessory, grantedSkillId: 7);
            var selected = MakeSkill(5);
            var helmSkill = MakeSkill(6);
            var accessorySkill = MakeSkill(7);

            var player = MakePlayer(level: 4, allocations: [Alloc(EAttribute.Strength, 3)], selectedSkills: [selected]);
            EquipInSlot(player, accessory, EEquipmentSlot.AccessorySlot);
            EquipInSlot(player, helm, EEquipmentSlot.HelmSlot);

            AssertBattlerParity(player,
                ItemResolver(helm, accessory),
                ThrowMod,
                SkillResolver(selected, helmSkill, accessorySkill));

            // Pin the exact ordered set so a divergence is reported directly rather than only via parity.
            var battler = BattleSnapshot.FromPlayer(player)
                .ToBattler(ItemResolver(helm, accessory), ThrowMod, SkillResolver(selected, helmSkill, accessorySkill),
                    resolveClass: ClassResolver(MakeClass(player.ClassId)));
            Assert.Equal([5, 6, 7], battler.Skills.Select(s => s.Skill.Id));
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static void AssertBattlerParity(Player player,
            Func<int, Item> resolveItem, Func<int, ItemMod> resolveMod, Func<int, Skill> resolveSkill)
        {
            // The live battler (Player.GetAllModifiers) composes only stat allocations + gear — the class locked
            // base is a snapshot-only contributor, like proficiency bonuses — so this stat/gear/skill round-trip
            // resolves the player's class to one with no attribute distributions (an empty locked base). The
            // locked-base composition itself is pinned by ToBattler_AppliesClassLockedBaseDistribution.
            var liveBattler = BattlerFactory.FromPlayer(player, resolveSkill);
            var snapshotBattler = BattleSnapshot.FromPlayer(player)
                .ToBattler(resolveItem, resolveMod, resolveSkill, resolveClass: ClassResolver(MakeClass(player.ClassId)));

            foreach (var attribute in Enum.GetValues<EAttribute>())
            {
                Assert.Equal(
                    liveBattler.GetAttributeValue(attribute),
                    snapshotBattler.GetAttributeValue(attribute));
            }

            Assert.Equal(liveBattler.Level, snapshotBattler.Level);
            Assert.Equal(liveBattler.CurrentHealth, snapshotBattler.CurrentHealth);
            Assert.Equal(
                liveBattler.Skills.Select(s => s.Skill.Id),
                snapshotBattler.Skills.Select(s => s.Skill.Id));
        }

        private static Player MakePlayer(int level = 1, List<StatAllocation>? allocations = null,
            List<Skill>? selectedSkills = null) =>
            new PlayerBuilder()
                .WithLevel(level)
                .WithStatAllocations(allocations ?? [])
                .WithSkills(selectedSkills ?? [])
                .WithSelectedSkills(selectedSkills ?? [])
                .Build();

        private static void Equip(Player player, Item item)
        {
            player.UnlockItem(item);
            player.TryEquipItem(item.Id, EEquipmentSlot.AccessorySlot, new Dictionary<int, int>());
        }

        private static void EquipInSlot(Player player, Item item, EEquipmentSlot slot)
        {
            player.UnlockItem(item);
            player.TryEquipItem(item.Id, slot, new Dictionary<int, int>());
        }

        private static void EquipWithMod(Player player, Item item, ItemMod mod, int modSlotIndex)
        {
            Equip(player, item);
            player.UnlockMod(mod.Id);
            player.TryApplyMod(item.Id, mod.Id, modSlotIndex, mod);
        }

        private static StatAllocation Alloc(EAttribute attribute, double amount) =>
            new() { Attribute = attribute, Amount = amount };

        private static Func<int, Item> ItemResolver(params Item[] items)
        {
            var map = items.ToDictionary(i => i.Id);
            return id => map[id];
        }

        private static Func<int, ItemMod> ModResolver(params ItemMod[] mods)
        {
            var map = mods.ToDictionary(m => m.Id);
            return id => map[id];
        }

        private static Func<int, Skill> SkillResolver(params Skill[] skills)
        {
            var map = skills.ToDictionary(s => s.Id);
            return id => map[id];
        }

        private static readonly Func<int, Item> ThrowItem =
            id => throw new InvalidOperationException($"Unexpected item resolve for {id}");
        private static readonly Func<int, ItemMod> ThrowMod =
            id => throw new InvalidOperationException($"Unexpected mod resolve for {id}");
        private static readonly Func<int, Skill> ThrowSkill =
            id => throw new InvalidOperationException($"Unexpected skill resolve for {id}");

        private static Func<int, Proficiency> ProficiencyResolver(params Proficiency[] proficiencies)
        {
            var map = proficiencies.ToDictionary(p => p.Id);
            return id => map[id];
        }

        private static Func<int, CoreClass> ClassResolver(params CoreClass[] classes)
        {
            var map = classes.ToDictionary(c => c.Id);
            return id => map[id];
        }

        /// <summary>A minimal class with the given locked-base distributions and a no-op signature passive.</summary>
        private static CoreClass MakeClass(int id, params AttributeDistribution[] distributions) => new()
        {
            Id = id,
            Name = $"Class {id}",
            StarterSkillIds = [],
            StarterEquipment = [],
            AttributeDistributions = distributions,
            SignaturePassive = new ClassSignaturePassive
            {
                Attribute = EAttribute.Strength,
                Amount = 0m,
                ScalingAttribute = null,
                ScalingAmount = 0m,
                ModifierType = EModifierType.Additive,
            },
        };

        /// <summary>A class carrying the given signature passive (and optional locked-base distributions).</summary>
        private static CoreClass MakeClassWithPassive(int id, ClassSignaturePassive passive, params AttributeDistribution[] distributions) => new()
        {
            Id = id,
            Name = $"Class {id}",
            StarterSkillIds = [],
            StarterEquipment = [],
            AttributeDistributions = distributions,
            SignaturePassive = passive,
        };

        private static AttributeDistribution Distribution(EAttribute attribute, decimal baseAmount, decimal amountPerLevel = 0m) =>
            new() { AttributeId = attribute, BaseAmount = baseAmount, AmountPerLevel = amountPerLevel };

        private static ProficiencyModifier ProfMod(EAttribute attribute, double amount) =>
            new() { Attribute = attribute, ModifierType = EModifierType.Additive, Amount = amount };

        private static Proficiency MakeProficiency(int id, params (int Level, ProficiencyModifier[] Modifiers)[] levels) => new()
        {
            Id = id,
            Name = $"Proficiency {id}",
            Description = string.Empty,
            PathId = 0,
            PathOrdinal = 0,
            MaxLevel = 10,
            BaseXp = 100,
            XpGrowth = 2,
            PrerequisiteIds = [],
            Levels = levels.Select(l => new ProficiencyLevel
            {
                Level = l.Level,
                Modifiers = l.Modifiers,
                RewardSkillId = null,
            }).ToList(),
        };

        private static Item MakeItem(int id, EItemCategory category = EItemCategory.Accessory,
            List<AttributeModifier>? attributes = null, List<ItemModSlot>? modSlots = null,
            int? grantedSkillId = null) => new()
            {
                Id = id,
                Name = $"Item {id}",
                Description = string.Empty,
                Category = category,
                Rarity = ERarity.Common,
                GrantedSkillId = grantedSkillId,
                Attributes = attributes ?? [],
                ModSlots = modSlots ?? [],
            };

        private static ItemMod MakeMod(int id, EItemModType type, List<AttributeModifier>? attributes = null) => new()
        {
            Id = id,
            Name = $"Mod {id}",
            Description = string.Empty,
            Type = type,
            Rarity = ERarity.Common,
            Attributes = attributes ?? [],
        };

        private static Skill MakeSkill(int id) => new()
        {
            Id = id,
            Name = $"Skill {id}",
            BaseDamage = 1,
            Description = string.Empty,
            DamageType = EDamageType.Physical,
            CooldownMs = 1000,
            DamageMultipliers = [],
            Effects = [],
        };

        private static AttributeModifier MakeModifier(EAttribute attribute, double amount) => new()
        {
            Attribute = attribute,
            Amount = amount,
            Type = EModifierType.Additive,
            Source = EAttributeModifierSource.Item,
        };
    }
}
