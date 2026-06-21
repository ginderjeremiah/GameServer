using Game.Abstractions.Contracts;
using Game.Abstractions.DataAccess;
using Game.Core;
using Game.DataAccess.Mapping;
using Xunit;
using CoreItem = Game.Core.Items.Item;
using CoreItemMod = Game.Core.Items.ItemMod;
using CoreSkill = Game.Core.Skills.Skill;
using EntityAppliedMod = Game.Infrastructure.Entities.AppliedMod;
using EntityLogPreference = Game.Infrastructure.Entities.LogPreference;
using EntityPlayer = Game.Infrastructure.Entities.Player;
using EntityPlayerAttribute = Game.Infrastructure.Entities.PlayerAttribute;
using EntityPlayerSkill = Game.Infrastructure.Entities.PlayerSkill;
using EntityUnlockedItem = Game.Infrastructure.Entities.UnlockedItem;
using EntityUnlockedMod = Game.Infrastructure.Entities.UnlockedMod;

namespace Game.Application.Tests.Mapping
{
    /// <summary>
    /// Parity-critical coverage for <see cref="PlayerMapper.ToCore"/>: the equipped-skill ordering
    /// (including the legacy <c>Order == 0</c> tie-break that feeds <c>BattleSnapshot.SkillIds</c>),
    /// the silent skip of an unknown equipment-slot id, and the per-item applied-mod grouping. A
    /// regression here surfaces only as a subtle battle-parity failure, so it is pinned directly.
    /// Also pins the loud fail-fast missing-reference policy (#1017): an owned item / item mod / skill
    /// that no longer resolves fails the whole load with a diagnosable <see cref="OrphanedReferenceException"/>
    /// naming the player, catalog, and missing id rather than silently dropping the player-owned row.
    /// </summary>
    public class PlayerMapperTests
    {
        [Fact]
        public void ToCore_OrdersSelectedSkillsByOrderThenSkillId_WithLegacyZeroTieBreak()
        {
            // Skills 6 and 7 share the legacy Order == 0 (tie-break is SkillId asc), skill 5 is Order 1,
            // skill 8 is unequipped. Expected equipped order: 6, 7 (Order 0, by id), then 5 (Order 1).
            var entity = BuildPlayer(
                skills:
                [
                    new() { SkillId = 5, Selected = true, Order = 1 },
                    new() { SkillId = 7, Selected = true, Order = 0 },
                    new() { SkillId = 6, Selected = true, Order = 0 },
                    new() { SkillId = 8, Selected = false, Order = 0 },
                ]);

            var player = PlayerMapper.ToCore(entity, Catalog(), Catalog(), Catalog());

            Assert.Equal([6, 7, 5], player.SelectedSkills.Select(s => s.Id));
            // Every unlocked skill is mapped regardless of selection.
            Assert.Equal([5, 6, 7, 8], player.Skills.Select(s => s.Id).OrderBy(id => id));
        }

        [Fact]
        public void ToCore_EquipsItemInResolvedSlot_AndSilentlySkipsUnknownSlotId()
        {
            var entity = BuildPlayer(
                unlockedItems:
                [
                    new() { ItemId = 10, EquipmentSlotId = (int)EEquipmentSlot.WeaponSlot },
                    // 99 resolves to no equipment slot, so the item is kept but not equipped.
                    new() { ItemId = 11, EquipmentSlotId = 99 },
                    new() { ItemId = 12, EquipmentSlotId = null },
                ]);

            var player = PlayerMapper.ToCore(entity, Catalog(), Catalog(), Catalog());

            var weaponSlot = player.Inventory.EquipmentSlots.Single(s => s.Value == EEquipmentSlot.WeaponSlot);
            Assert.Equal(10, weaponSlot.ItemId);

            // The unknown-slot item is present in the inventory but equipped in no slot.
            Assert.Contains(player.Inventory.UnlockedItems, ui => ui.ItemId == 11);
            Assert.DoesNotContain(player.Inventory.EquipmentSlots, s => s.ItemId == 11);
        }

        [Fact]
        public void ToCore_GroupsAppliedModsByItem()
        {
            var entity = BuildPlayer(
                unlockedItems:
                [
                    new() { ItemId = 10, EquipmentSlotId = null },
                    new() { ItemId = 11, EquipmentSlotId = null },
                ],
                appliedMods:
                [
                    new() { ItemId = 10, ItemModSlotId = 0, ItemModId = 100 },
                    new() { ItemId = 10, ItemModSlotId = 1, ItemModId = 101 },
                    new() { ItemId = 11, ItemModSlotId = 0, ItemModId = 102 },
                ]);

            var player = PlayerMapper.ToCore(entity, Catalog(), Catalog(), Catalog());

            var item10 = player.Inventory.UnlockedItems.Single(ui => ui.ItemId == 10);
            var item11 = player.Inventory.UnlockedItems.Single(ui => ui.ItemId == 11);

            Assert.Equal([100, 101], item10.AppliedMods.Select(m => m.ItemModId).OrderBy(id => id));
            Assert.Equal([102], item11.AppliedMods.Select(m => m.ItemModId));
            // Each applied mod resolves its domain model from the cached catalog.
            Assert.All(item10.AppliedMods, m => Assert.Equal(m.ItemModId, m.ItemMod.Id));
        }

        [Fact]
        public void ToCore_MapsScalarFields_StatAllocations_UnlockedMods_AndLogPreferences()
        {
            var entity = BuildPlayer(
                attributes:
                [
                    new() { AttributeId = (int)EAttribute.Strength, Amount = 5m },
                    new() { AttributeId = (int)EAttribute.Agility, Amount = 3m },
                ],
                unlockedMods: [new() { ItemModId = 100 }, new() { ItemModId = 101 }],
                logPreferences: [new() { LogTypeId = (int)ELogType.Damage, Enabled = false }]);

            var player = PlayerMapper.ToCore(entity, Catalog(), Catalog(), Catalog());

            Assert.Equal(1, player.Id);
            Assert.Equal("Hero", player.Name);
            Assert.Equal(3, player.Level);
            Assert.Equal(MappedLastActivity, player.LastActivity);
            // No boss mode authored on the entity ⇒ idle (false) on the domain model.
            Assert.False(player.AutoChallengeBoss);
            Assert.Contains(100, player.Inventory.UnlockedMods);
            Assert.Contains(101, player.Inventory.UnlockedMods);
            var strength = player.StatPoints.StatAllocations.Single(a => a.Attribute == EAttribute.Strength);
            Assert.Equal(5d, strength.Amount);
            var pref = Assert.Single(player.LogPreferences);
            Assert.Equal(ELogType.Damage, pref.LogType);
            Assert.False(pref.Enabled);
        }

        [Fact]
        public void ToCore_MapsAutoChallengeBoss_WhenInBossMode()
        {
            // A persisted boss-mode player carries the auto-challenge-boss flag, which ToCore must map so
            // the offline-rewards sim can resume the boss loop (in CurrentZoneId) at next login.
            var entity = BuildPlayer(autoChallengeBoss: true);

            var player = PlayerMapper.ToCore(entity, Catalog(), Catalog(), Catalog());

            Assert.True(player.AutoChallengeBoss);
        }

        [Fact]
        public void ToCore_OrphanedItemReference_ThrowsDiagnosableException()
        {
            // Player 1 owns item 11, which the catalog can no longer resolve (a content-data mistake).
            var entity = BuildPlayer(unlockedItems: [new() { ItemId = 11, EquipmentSlotId = null }]);
            var catalog = Catalog(missingItemIds: [11]);

            var ex = Assert.Throws<OrphanedReferenceException>(
                () => PlayerMapper.ToCore(entity, catalog, catalog, catalog));

            // The message names the player, the catalog, and the missing id so the mistake is diagnosable from logs.
            Assert.Contains("Player 1", ex.Message);
            Assert.Contains("item", ex.Message);
            Assert.Contains("11", ex.Message);
            // The originating catalog failure is preserved as the inner exception rather than swallowed.
            Assert.NotNull(ex.InnerException);
        }

        [Fact]
        public void ToCore_OrphanedItemModReference_ThrowsDiagnosableException()
        {
            // Item 10 resolves, but its applied mod 100 no longer does.
            var entity = BuildPlayer(
                unlockedItems: [new() { ItemId = 10, EquipmentSlotId = null }],
                appliedMods: [new() { ItemId = 10, ItemModSlotId = 0, ItemModId = 100 }]);
            var catalog = Catalog(missingItemModIds: [100]);

            var ex = Assert.Throws<OrphanedReferenceException>(
                () => PlayerMapper.ToCore(entity, catalog, catalog, catalog));

            Assert.Contains("Player 1", ex.Message);
            Assert.Contains("item mod", ex.Message);
            Assert.Contains("100", ex.Message);
            Assert.NotNull(ex.InnerException);
        }

        [Fact]
        public void ToCore_OrphanedSkillReference_ThrowsDiagnosableException()
        {
            var entity = BuildPlayer(skills: [new() { SkillId = 7, Selected = true, Order = 0 }]);
            var catalog = Catalog(missingSkillIds: [7]);

            var ex = Assert.Throws<OrphanedReferenceException>(
                () => PlayerMapper.ToCore(entity, catalog, catalog, catalog));

            Assert.Contains("Player 1", ex.Message);
            Assert.Contains("skill", ex.Message);
            Assert.Contains("7", ex.Message);
            Assert.NotNull(ex.InnerException);
        }

        private static readonly DateTime MappedLastActivity = new(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc);

        private static EntityPlayer BuildPlayer(
            List<EntityPlayerSkill>? skills = null,
            List<EntityUnlockedItem>? unlockedItems = null,
            List<EntityAppliedMod>? appliedMods = null,
            List<EntityUnlockedMod>? unlockedMods = null,
            List<EntityPlayerAttribute>? attributes = null,
            List<EntityLogPreference>? logPreferences = null,
            bool autoChallengeBoss = false) => new()
            {
                Id = 1,
                Name = "Hero",
                Level = 3,
                Exp = 0,
                CurrentZoneId = 0,
                StatPointsGained = 0,
                StatPointsUsed = 0,
                LastActivity = MappedLastActivity,
                AutoChallengeBoss = autoChallengeBoss,
                PlayerSkills = skills ?? [],
                UnlockedItems = unlockedItems ?? [],
                AppliedMods = appliedMods ?? [],
                UnlockedMods = unlockedMods ?? [],
                PlayerAttributes = attributes ?? [],
                LogPreferences = logPreferences ?? [],
            };

        private static InMemoryCatalog Catalog(
            int[]? missingItemIds = null,
            int[]? missingItemModIds = null,
            int[]? missingSkillIds = null) => new(missingItemIds, missingItemModIds, missingSkillIds);

        /// <summary>
        /// A trivial in-memory stand-in for the reference-data caches. ToCore only resolves a domain
        /// model by id, so this builds one on demand rather than depending on the database-backed cache.
        /// Designated "missing" ids throw the same descriptive <see cref="ArgumentOutOfRangeException"/> the
        /// real catalog raises for an unresolvable id, exercising the orphaned-reference path.
        /// </summary>
        private sealed class InMemoryCatalog(
            int[]? missingItemIds,
            int[]? missingItemModIds,
            int[]? missingSkillIds) : IItems, IItemMods, ISkills
        {
            private readonly HashSet<int> _missingItemIds = [.. missingItemIds ?? []];
            private readonly HashSet<int> _missingItemModIds = [.. missingItemModIds ?? []];
            private readonly HashSet<int> _missingSkillIds = [.. missingSkillIds ?? []];

            public CoreItem GetItem(int itemId)
            {
                ThrowIfMissing(_missingItemIds, itemId, "item");
                return new()
                {
                    Id = itemId,
                    Name = $"Item {itemId}",
                    Description = string.Empty,
                    Category = EItemCategory.Weapon,
                    Rarity = ERarity.Common,
                    Attributes = [],
                    ModSlots = [],
                };
            }

            public CoreItemMod GetItemMod(int itemModId)
            {
                ThrowIfMissing(_missingItemModIds, itemModId, "item mod");
                return new()
                {
                    Id = itemModId,
                    Name = $"Mod {itemModId}",
                    Description = string.Empty,
                    Type = EItemModType.Component,
                    Rarity = ERarity.Common,
                    Attributes = [],
                };
            }

            public CoreSkill GetSkill(int skillId)
            {
                ThrowIfMissing(_missingSkillIds, skillId, "skill");
                return new()
                {
                    Id = skillId,
                    Name = $"Skill {skillId}",
                    BaseDamage = 1,
                    Description = string.Empty,
                    CooldownMs = 1000,
                    DamageMultipliers = [],
                    Effects = [],
                };
            }

            public bool ValidateItemModId(int itemModId) => true;

            // ToCore never reads the full contract lists or the version key, only the per-id resolvers above.
            List<Item> IItems.All() => throw new NotSupportedException();
            List<ItemMod> IItemMods.All() => throw new NotSupportedException();
            List<Skill> ISkills.AllSkills() => throw new NotSupportedException();
            object IItems.VersionKey => throw new NotSupportedException();
            object IItemMods.VersionKey => throw new NotSupportedException();
            object ISkills.VersionKey => throw new NotSupportedException();

            // Mirrors the real catalog's descriptive out-of-range failure for an unresolvable id.
            private static void ThrowIfMissing(HashSet<int> missingIds, int id, string setName)
            {
                if (missingIds.Contains(id))
                {
                    throw new ArgumentOutOfRangeException(nameof(id), id, $"No {setName} exists with Id {id}.");
                }
            }
        }
    }
}
