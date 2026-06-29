using Game.Abstractions.Contracts;
using Game.Abstractions.DataAccess;
using Game.Core;
using Game.Core.Players;
using Game.Core.Players.Inventories;
using Game.DataAccess.Mapping;
using Xunit;
using CoreItem = Game.Core.Items.Item;
using CoreItemMod = Game.Core.Items.ItemMod;
using CoreSkill = Game.Core.Skills.Skill;

namespace Game.Application.Tests.Mapping
{
    /// <summary>
    /// Covers the lean player cache model (#1155): <see cref="PlayerCacheMapper.ToCore"/> rehydrates the
    /// aggregate, re-resolving reference data from the in-memory catalogs (so the cache never serves a stale
    /// snapshot), and <see cref="PlayerCacheMapper.ToCacheModel"/> reduces every owned reference to ids before
    /// serialization. Parity-critical behaviour is pinned directly here because a regression surfaces only as a
    /// subtle battle-parity failure: the equipped-skill ordering (including the legacy <c>Order == 0</c>
    /// tie-break that feeds <c>BattleSnapshot.SkillIds</c>), the silent skip of an unknown equipment-slot id,
    /// and the per-item applied-mod grouping. The loud fail-fast missing-reference policy (#1017) is pinned too:
    /// an owned item / item mod / skill that no longer resolves fails the whole load with a diagnosable
    /// <see cref="OrphanedReferenceException"/> rather than silently dropping the player-owned row.
    /// </summary>
    public class PlayerCacheMapperTests
    {
        [Fact]
        public void ToCore_OrdersSelectedSkillsByOrderThenSkillId_WithLegacyZeroTieBreak()
        {
            // Skills 6 and 7 share the legacy Order == 0 (tie-break is SkillId asc), skill 5 is Order 1,
            // skill 8 is unequipped. Expected equipped order: 6, 7 (Order 0, by id), then 5 (Order 1).
            var model = BuildModel(
                skills:
                [
                    new() { SkillId = 5, Selected = true, Order = 1 },
                    new() { SkillId = 7, Selected = true, Order = 0 },
                    new() { SkillId = 6, Selected = true, Order = 0 },
                    new() { SkillId = 8, Selected = false, Order = 0 },
                ]);

            var player = PlayerCacheMapper.ToCore(model, Catalog(), Catalog(), Catalog());

            Assert.Equal([6, 7, 5], player.SelectedSkills.Select(s => s.Id));
            // Every unlocked skill is mapped regardless of selection.
            Assert.Equal([5, 6, 7, 8], player.Skills.Select(s => s.Id).OrderBy(id => id));
        }

        [Fact]
        public void ToCore_EquipsItemInResolvedSlot_AndSilentlySkipsUnknownSlotId()
        {
            var model = BuildModel(
                unlockedItems:
                [
                    new() { ItemId = 10, EquipmentSlotId = (int)EEquipmentSlot.WeaponSlot, Favorite = false },
                    // 99 resolves to no equipment slot, so the item is kept but not equipped.
                    new() { ItemId = 11, EquipmentSlotId = 99, Favorite = false },
                    new() { ItemId = 12, EquipmentSlotId = null, Favorite = false },
                ]);

            var player = PlayerCacheMapper.ToCore(model, Catalog(), Catalog(), Catalog());

            var weaponSlot = player.Inventory.EquipmentSlots.Single(s => s.Value == EEquipmentSlot.WeaponSlot);
            Assert.Equal(10, weaponSlot.ItemId);

            // The unknown-slot item is present in the inventory but equipped in no slot.
            Assert.Contains(player.Inventory.UnlockedItems, ui => ui.ItemId == 11);
            Assert.DoesNotContain(player.Inventory.EquipmentSlots, s => s.ItemId == 11);
        }

        [Fact]
        public void ToCore_GroupsAppliedModsByItem()
        {
            var model = BuildModel(
                unlockedItems:
                [
                    new() { ItemId = 10, EquipmentSlotId = null, Favorite = false },
                    new() { ItemId = 11, EquipmentSlotId = null, Favorite = false },
                ],
                appliedMods:
                [
                    new() { ItemId = 10, ItemModSlotId = 0, ItemModId = 100 },
                    new() { ItemId = 10, ItemModSlotId = 1, ItemModId = 101 },
                    new() { ItemId = 11, ItemModSlotId = 0, ItemModId = 102 },
                ]);

            var player = PlayerCacheMapper.ToCore(model, Catalog(), Catalog(), Catalog());

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
            var model = BuildModel(
                statAllocations:
                [
                    new() { Attribute = EAttribute.Strength, Amount = 5d },
                    new() { Attribute = EAttribute.Agility, Amount = 3d },
                ],
                unlockedModIds: [100, 101],
                logPreferences: [new() { LogType = ELogType.Damage, Enabled = false }]);

            var player = PlayerCacheMapper.ToCore(model, Catalog(), Catalog(), Catalog());

            Assert.Equal(1, player.Id);
            Assert.Equal(2, player.ClassId);
            Assert.Equal("Hero", player.Name);
            Assert.Equal(3, player.Level);
            Assert.Equal(MappedLastActivity, player.LastActivity);
            // No boss mode authored on the model ⇒ idle (false) on the domain model.
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
            var model = BuildModel(autoChallengeBoss: true);

            var player = PlayerCacheMapper.ToCore(model, Catalog(), Catalog(), Catalog());

            Assert.True(player.AutoChallengeBoss);
        }

        [Fact]
        public void ToCore_OrphanedItemReference_ThrowsDiagnosableException()
        {
            // Player 1 owns item 11, which the catalog can no longer resolve (a content-data mistake).
            var model = BuildModel(unlockedItems: [new() { ItemId = 11, EquipmentSlotId = null, Favorite = false }]);
            var catalog = Catalog(missingItemIds: [11]);

            var ex = Assert.Throws<OrphanedReferenceException>(
                () => PlayerCacheMapper.ToCore(model, catalog, catalog, catalog));

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
            var model = BuildModel(
                unlockedItems: [new() { ItemId = 10, EquipmentSlotId = null, Favorite = false }],
                appliedMods: [new() { ItemId = 10, ItemModSlotId = 0, ItemModId = 100 }]);
            var catalog = Catalog(missingItemModIds: [100]);

            var ex = Assert.Throws<OrphanedReferenceException>(
                () => PlayerCacheMapper.ToCore(model, catalog, catalog, catalog));

            Assert.Contains("Player 1", ex.Message);
            Assert.Contains("item mod", ex.Message);
            Assert.Contains("100", ex.Message);
            Assert.NotNull(ex.InnerException);
        }

        [Fact]
        public void ToCore_OrphanedSkillReference_ThrowsDiagnosableException()
        {
            var model = BuildModel(skills: [new() { SkillId = 7, Selected = true, Order = 0 }]);
            var catalog = Catalog(missingSkillIds: [7]);

            var ex = Assert.Throws<OrphanedReferenceException>(
                () => PlayerCacheMapper.ToCore(model, catalog, catalog, catalog));

            Assert.Contains("Player 1", ex.Message);
            Assert.Contains("skill", ex.Message);
            Assert.Contains("7", ex.Message);
            Assert.NotNull(ex.InnerException);
        }

        [Fact]
        public void ToCore_ResolvesReferenceData_FromCatalog_NotFromModel()
        {
            // The model carries no reference data at all — only ids — so a resolved item/mod/skill can only
            // have come from the catalog. This is the whole point of the lean model: the cache can never serve
            // a stale reference snapshot because it never holds one.
            var model = BuildModel(
                unlockedItems: [new() { ItemId = 10, EquipmentSlotId = null, Favorite = false }],
                appliedMods: [new() { ItemId = 10, ItemModSlotId = 0, ItemModId = 100 }],
                skills: [new() { SkillId = 7, Selected = true, Order = 0 }]);

            var player = PlayerCacheMapper.ToCore(model, Catalog(), Catalog(), Catalog());

            var item = player.Inventory.UnlockedItems.Single(ui => ui.ItemId == 10);
            Assert.Equal("Item 10", item.Item.Name);
            Assert.Equal("Mod 100", item.AppliedMods.Single().ItemMod.Name);
            Assert.Equal("Skill 7", player.Skills.Single().Name);
        }

        [Fact]
        public void ToCacheModel_ReducesReferenceDataToIds_PreservingPlayerSpecificState()
        {
            var catalog = Catalog();
            var player = BuildDomainPlayer(catalog);

            var model = PlayerCacheMapper.ToCacheModel(player);

            // Equipment slot and favorite are captured against the unlocked item; the equipped item is 10.
            var item10 = model.UnlockedItems.Single(ui => ui.ItemId == 10);
            Assert.Equal((int)EEquipmentSlot.WeaponSlot, item10.EquipmentSlotId);
            Assert.True(item10.Favorite);
            var item11 = model.UnlockedItems.Single(ui => ui.ItemId == 11);
            Assert.Null(item11.EquipmentSlotId);

            // Applied mods are flattened against their item id.
            var mod = Assert.Single(model.AppliedMods);
            Assert.Equal(10, mod.ItemId);
            Assert.Equal(100, mod.ItemModId);
            Assert.Equal(0, mod.ItemModSlotId);

            Assert.Equal([200, 201], model.UnlockedModIds.OrderBy(id => id));

            // The equipped loadout order (6 then 7) is captured as per-skill Selected/Order flags.
            Assert.Equal(0, model.Skills.Single(s => s.SkillId == 6).Order);
            Assert.True(model.Skills.Single(s => s.SkillId == 6).Selected);
            Assert.Equal(1, model.Skills.Single(s => s.SkillId == 7).Order);
            Assert.True(model.Skills.Single(s => s.SkillId == 7).Selected);
            Assert.False(model.Skills.Single(s => s.SkillId == 5).Selected);
        }

        [Fact]
        public void ToCacheModel_SerializedBlob_ExcludesReferenceData()
        {
            var catalog = Catalog();
            var player = BuildDomainPlayer(catalog);

            var json = PlayerCacheMapper.ToCacheModel(player).Serialize();

            // The blob carries the owned ids...
            Assert.Contains("\"itemId\":10", json);
            Assert.Contains("\"skillId\":6", json);
            // ...but none of the reference-data fields the full Item/Skill/ItemMod graphs would have dragged in.
            Assert.DoesNotContain("baseDamage", json);
            Assert.DoesNotContain("cooldownMs", json);
            Assert.DoesNotContain("modSlots", json);
            Assert.DoesNotContain("description", json);
        }

        [Fact]
        public void RoundTrip_ThroughSerializedModel_PreservesPlayerState()
        {
            var catalog = Catalog();
            var player = BuildDomainPlayer(catalog);

            // Round-trip exactly as the cache does: project to the lean model, serialize, deserialize, rehydrate.
            var json = PlayerCacheMapper.ToCacheModel(player).Serialize();
            var model = json.Deserialize<PlayerCacheModel>();
            Assert.NotNull(model);
            var rehydrated = PlayerCacheMapper.ToCore(model, catalog, catalog, catalog);

            Assert.Equal(player.Id, rehydrated.Id);
            Assert.Equal(player.ClassId, rehydrated.ClassId);
            Assert.Equal(player.Name, rehydrated.Name);
            Assert.Equal(player.Level, rehydrated.Level);
            Assert.Equal(player.Exp, rehydrated.Exp);
            Assert.Equal(player.CurrentZoneId, rehydrated.CurrentZoneId);
            Assert.Equal(player.LastActivity, rehydrated.LastActivity);
            Assert.True(rehydrated.AutoChallengeBoss);
            Assert.Equal(player.StatPoints.StatPointsGained, rehydrated.StatPoints.StatPointsGained);
            Assert.Equal(player.StatPoints.StatPointsUsed, rehydrated.StatPoints.StatPointsUsed);
            Assert.Equal(5d, rehydrated.StatPoints.StatAllocations.Single(a => a.Attribute == EAttribute.Strength).Amount);

            // Inventory: unlocked items, the equipped weapon, the favorite flag, the applied mod, unlocked mods.
            Assert.Equal([10, 11], rehydrated.Inventory.UnlockedItems.Select(ui => ui.ItemId).OrderBy(id => id));
            var weapon = rehydrated.Inventory.EquipmentSlots.Single(s => s.Value == EEquipmentSlot.WeaponSlot);
            Assert.Equal(10, weapon.ItemId);
            var rehydratedItem10 = rehydrated.Inventory.UnlockedItems.Single(ui => ui.ItemId == 10);
            Assert.True(rehydratedItem10.Favorite);
            Assert.Equal(100, rehydratedItem10.AppliedMods.Single().ItemModId);
            Assert.Equal([200, 201], rehydrated.Inventory.UnlockedMods.OrderBy(id => id));

            // Skills: full unlocked set and the equipped loadout in its original order.
            Assert.Equal([5, 6, 7], rehydrated.Skills.Select(s => s.Id).OrderBy(id => id));
            Assert.Equal([6, 7], rehydrated.SelectedSkills.Select(s => s.Id));

            var pref = Assert.Single(rehydrated.LogPreferences);
            Assert.Equal(ELogType.Damage, pref.LogType);
            Assert.False(pref.Enabled);
        }

        private static readonly DateTime MappedLastActivity = new(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc);

        private static PlayerCacheModel BuildModel(
            List<CachedPlayerSkill>? skills = null,
            List<CachedUnlockedItem>? unlockedItems = null,
            List<CachedAppliedMod>? appliedMods = null,
            List<int>? unlockedModIds = null,
            List<StatAllocation>? statAllocations = null,
            List<LogPreference>? logPreferences = null,
            bool autoChallengeBoss = false) => new()
            {
                Id = 1,
                ClassId = 2,
                Name = "Hero",
                Level = 3,
                Exp = 0,
                CurrentZoneId = 0,
                StatPointsGained = 0,
                StatPointsUsed = 0,
                LastActivity = MappedLastActivity,
                AutoChallengeBoss = autoChallengeBoss,
                StatAllocations = statAllocations ?? [],
                UnlockedItems = unlockedItems ?? [],
                AppliedMods = appliedMods ?? [],
                UnlockedModIds = unlockedModIds ?? [],
                Skills = skills ?? [],
                LogPreferences = logPreferences ?? [],
            };

        /// <summary>
        /// Builds a domain player with a representative spread of owned state: an equipped + favorited item
        /// carrying an applied mod, a second unequipped item, unlocked mods, and a selected skill loadout in a
        /// deliberate order. The reference instances are pulled from <paramref name="catalog"/> so ToCacheModel
        /// only ever has ids to capture.
        /// </summary>
        private static Player BuildDomainPlayer(InMemoryCatalog catalog)
        {
            var item10 = catalog.GetItem(10);
            var item11 = catalog.GetItem(11);
            var mod100 = catalog.GetItemMod(100);

            var inventory = new Inventory();
            inventory.UnlockedMods.Add(200);
            inventory.UnlockedMods.Add(201);
            inventory.UnlockedItems =
            [
                new UnlockedItemSlot
                {
                    Item = item10,
                    Favorite = true,
                    AppliedMods = [new AppliedModSlot { ItemModId = 100, ItemModSlotId = 0, ItemMod = mod100 }],
                },
                new UnlockedItemSlot { Item = item11, Favorite = false, AppliedMods = [] },
            ];
            inventory.EquipmentSlots.Single(s => s.Value == EEquipmentSlot.WeaponSlot).Set(item10);

            var skill5 = catalog.GetSkill(5);
            var skill6 = catalog.GetSkill(6);
            var skill7 = catalog.GetSkill(7);

            return new Player
            {
                Id = 1,
                ClassId = 2,
                Name = "Hero",
                Level = 3,
                Exp = 12,
                CurrentZoneId = 4,
                LastActivity = MappedLastActivity,
                AutoChallengeBoss = true,
                StatPoints = new PlayerStatPoints
                {
                    StatPointsGained = 10,
                    StatPointsUsed = 8,
                    StatAllocations = [new StatAllocation { Attribute = EAttribute.Strength, Amount = 5d }],
                },
                Inventory = inventory,
                Skills = [skill5, skill6, skill7],
                SelectedSkills = [skill6, skill7],
                LogPreferences = [new LogPreference { LogType = ELogType.Damage, Enabled = false }],
            };
        }

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
                    DamageType = EDamageType.Physical,
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
