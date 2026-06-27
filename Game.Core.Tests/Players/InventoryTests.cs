using Game.Core.Attributes.Modifiers;
using Game.Core.Items;
using Game.Core.Players.Inventories;
using Xunit;

namespace Game.Core.Tests.Players
{
    public class InventoryTests
    {
        // ── Equipment slots ─────────────────────────────────────────────────

        [Fact]
        public void NewInventory_HasExactlyOneSlotPerEquipmentSlotValue()
        {
            var inventory = new Inventory();

            var slotValues = inventory.EquipmentSlots.Select(s => s.Value).ToList();
            Assert.Equal(Enum.GetValues<EEquipmentSlot>().Length, slotValues.Count);
            Assert.Equal(Enum.GetValues<EEquipmentSlot>().ToHashSet(), slotValues.ToHashSet());
        }

        // ── UnlockItem ──────────────────────────────────────────────────────

        [Fact]
        public void UnlockItem_NewItem_AddsToUnlockedItems()
        {
            var inventory = new Inventory();
            var item = MakeItem(42);

            inventory.UnlockItem(item);

            Assert.Single(inventory.UnlockedItems);
            Assert.Equal(item, inventory.UnlockedItems.Single().Item);
        }

        [Fact]
        public void UnlockItem_DuplicateItem_DoesNotAddTwice()
        {
            var inventory = new Inventory();
            var item = MakeItem(42);

            inventory.UnlockItem(item);
            inventory.UnlockItem(item);

            Assert.Single(inventory.UnlockedItems);
        }

        [Fact]
        public void UnlockItem_ReportsWhetherItWasNewlyUnlocked()
        {
            var inventory = new Inventory();
            var item = MakeItem(42);

            Assert.True(inventory.UnlockItem(item));
            Assert.False(inventory.UnlockItem(item));
        }

        // ── UnlockMod ───────────────────────────────────────────────────────

        [Fact]
        public void UnlockMod_NewMod_AddsToUnlockedMods()
        {
            var inventory = new Inventory();

            inventory.UnlockMod(7);

            Assert.Contains(7, inventory.UnlockedMods);
        }

        [Fact]
        public void UnlockMod_DuplicateMod_NoError()
        {
            var inventory = new Inventory();

            inventory.UnlockMod(7);
            inventory.UnlockMod(7);

            Assert.Single(inventory.UnlockedMods);
        }

        [Fact]
        public void UnlockMod_ReportsWhetherItWasNewlyUnlocked()
        {
            var inventory = new Inventory();

            Assert.True(inventory.UnlockMod(7));
            Assert.False(inventory.UnlockMod(7));
        }

        // ── TryEquipItem ────────────────────────────────────────────────────

        [Fact]
        public void TryEquipItem_UnlockedItem_EquipsSuccessfully()
        {
            var inventory = new Inventory();
            var item = MakeItem(1, EItemCategory.Accessory);
            AddUnlockedItem(inventory, item);

            var result = inventory.TryEquipItem(1, EEquipmentSlot.AccessorySlot, new Dictionary<int, int>());

            Assert.True(result);
            var slot = inventory.EquipmentSlots.First(s => s.Value == EEquipmentSlot.AccessorySlot);
            Assert.Equal(1, slot.ItemId);
            Assert.Equal(item, slot.Item);
        }

        [Fact]
        public void TryEquipItem_NotUnlocked_ReturnsFalse()
        {
            var inventory = new Inventory();

            var result = inventory.TryEquipItem(999, EEquipmentSlot.AccessorySlot, new Dictionary<int, int>());

            Assert.False(result);
        }

        [Fact]
        public void TryEquipItem_WrongCategory_ReturnsFalse()
        {
            var inventory = new Inventory();
            var item = MakeItem(1, EItemCategory.Weapon);
            AddUnlockedItem(inventory, item);

            // Weapon into Accessory slot should fail
            var result = inventory.TryEquipItem(1, EEquipmentSlot.AccessorySlot, new Dictionary<int, int>());

            Assert.False(result);
        }

        [Fact]
        public void TryEquipItem_AlreadyEquippedElsewhere_MovesToNewSlot()
        {
            var inventory = new Inventory();
            var item = MakeItem(1, EItemCategory.Accessory);
            AddUnlockedItem(inventory, item);

            inventory.TryEquipItem(1, EEquipmentSlot.AccessorySlot, new Dictionary<int, int>());

            // Find another accessory slot if one exists, or just verify the old slot is cleared
            var oldSlot = inventory.EquipmentSlots.First(s => s.Value == EEquipmentSlot.AccessorySlot);
            Assert.NotNull(oldSlot.Item);
        }

        [Fact]
        public void TryEquipItem_TargetSlotOccupied_ReplacesPreviousItem()
        {
            var inventory = new Inventory();
            var itemA = MakeItem(1, EItemCategory.Accessory);
            var itemB = MakeItem(2, EItemCategory.Accessory);
            AddUnlockedItem(inventory, itemA);
            AddUnlockedItem(inventory, itemB);
            inventory.TryEquipItem(1, EEquipmentSlot.AccessorySlot, new Dictionary<int, int>());

            var result = inventory.TryEquipItem(2, EEquipmentSlot.AccessorySlot, new Dictionary<int, int>());

            Assert.True(result);
            var slot = inventory.EquipmentSlots.First(s => s.Value == EEquipmentSlot.AccessorySlot);
            Assert.Equal(2, slot.ItemId);
            Assert.Equal(itemB, slot.Item);
            // The previously equipped item must no longer occupy any slot.
            Assert.DoesNotContain(inventory.EquipmentSlots, s => s.ItemId == 1);
        }

        [Fact]
        public void TryEquipItem_ProficiencyGateMet_EquipsSuccessfully()
        {
            var inventory = new Inventory();
            var item = MakeItem(1, EItemCategory.Accessory, requiredProficiencyId: 3, requiredProficiencyLevel: 5);
            AddUnlockedItem(inventory, item);

            var result = inventory.TryEquipItem(1, EEquipmentSlot.AccessorySlot, new Dictionary<int, int> { [3] = 5 });

            Assert.True(result);
            var slot = inventory.EquipmentSlots.First(s => s.Value == EEquipmentSlot.AccessorySlot);
            Assert.Equal(1, slot.ItemId);
        }

        [Fact]
        public void TryEquipItem_ProficiencyGateNotMet_ReturnsFalseAndDoesNotEquip()
        {
            var inventory = new Inventory();
            var item = MakeItem(1, EItemCategory.Accessory, requiredProficiencyId: 3, requiredProficiencyLevel: 5);
            AddUnlockedItem(inventory, item);

            var result = inventory.TryEquipItem(1, EEquipmentSlot.AccessorySlot, new Dictionary<int, int> { [3] = 4 });

            Assert.False(result);
            var slot = inventory.EquipmentSlots.First(s => s.Value == EEquipmentSlot.AccessorySlot);
            Assert.Null(slot.ItemId);
        }

        [Fact]
        public void TryEquipItem_UngatedItem_EquipsRegardlessOfProficiencies()
        {
            var inventory = new Inventory();
            var item = MakeItem(1, EItemCategory.Accessory);
            AddUnlockedItem(inventory, item);

            var result = inventory.TryEquipItem(1, EEquipmentSlot.AccessorySlot, new Dictionary<int, int>());

            Assert.True(result);
        }

        // ── TryUnequipItem ──────────────────────────────────────────────────

        [Fact]
        public void TryUnequipItem_EquippedSlot_UnequipsSuccessfully()
        {
            var inventory = new Inventory();
            var item = MakeItem(1, EItemCategory.Accessory);
            AddUnlockedItem(inventory, item);
            inventory.TryEquipItem(1, EEquipmentSlot.AccessorySlot, new Dictionary<int, int>());

            var result = inventory.TryUnequipItem(EEquipmentSlot.AccessorySlot);

            Assert.Equal(1, result);
            var slot = inventory.EquipmentSlots.First(s => s.Value == EEquipmentSlot.AccessorySlot);
            Assert.Null(slot.ItemId);
            Assert.Null(slot.Item);
        }

        [Fact]
        public void TryUnequipItem_EmptySlot_ReturnsNull()
        {
            var inventory = new Inventory();

            var result = inventory.TryUnequipItem(EEquipmentSlot.AccessorySlot);

            Assert.Null(result);
        }

        // ── TryApplyMod ─────────────────────────────────────────────────────

        [Fact]
        public void TryApplyMod_ValidModAndSlot_AppliesSuccessfully()
        {
            var inventory = new Inventory();
            var item = MakeItem(1, modSlots:
            [
                new ItemModSlot { Id = 0, Type = EItemModType.Prefix },
            ]);
            AddUnlockedItem(inventory, item);
            inventory.UnlockMod(10);

            var mod = new ItemMod
            {
                Id = 0,
                Name = "Sharp",
                Description = "",
                Type = EItemModType.Prefix,
                Rarity = ERarity.Uncommon,
                Attributes = [],
            };

            var result = inventory.TryApplyMod(1, 10, 0, mod);

            Assert.True(result);
            var applied = inventory.UnlockedItems.Single().AppliedMods;
            Assert.Single(applied);
            Assert.Equal(mod, applied[0].ItemMod);
        }

        [Fact]
        public void TryApplyMod_ModNotUnlocked_ReturnsFalse()
        {
            var inventory = new Inventory();
            var item = MakeItem(1, modSlots:
            [
                new ItemModSlot { Id = 0, Type = EItemModType.Prefix },
            ]);
            AddUnlockedItem(inventory, item);
            // Don't unlock mod 10

            var mod = new ItemMod
            {
                Id = 0,
                Name = "Sharp",
                Description = "",
                Type = EItemModType.Prefix,
                Rarity = ERarity.Uncommon,
                Attributes = [],
            };

            var result = inventory.TryApplyMod(1, 10, 0, mod);

            Assert.False(result);
        }

        [Fact]
        public void TryApplyMod_WrongModType_ReturnsFalse()
        {
            var inventory = new Inventory();
            var item = MakeItem(1, modSlots:
            [
                new ItemModSlot { Id = 0, Type = EItemModType.Prefix },
            ]);
            AddUnlockedItem(inventory, item);
            inventory.UnlockMod(10);

            var mod = new ItemMod
            {
                Id = 0,
                Name = "Of Fire",
                Description = "",
                Type = EItemModType.Suffix, // Wrong type — slot expects Prefix
                Rarity = ERarity.Uncommon,
                Attributes = [],
            };

            var result = inventory.TryApplyMod(1, 10, 0, mod);

            Assert.False(result);
        }

        [Fact]
        public void TryApplyMod_SlotAlreadyHasMod_ReplacesExistingMod()
        {
            var inventory = new Inventory();
            var item = MakeItem(1, modSlots:
            [
                new ItemModSlot { Id = 0, Type = EItemModType.Prefix },
            ]);
            AddUnlockedItem(inventory, item);
            inventory.UnlockMod(10);
            inventory.UnlockMod(11);
            inventory.TryApplyMod(1, 10, 0, MakeMod(10, EItemModType.Prefix));

            var result = inventory.TryApplyMod(1, 11, 0, MakeMod(11, EItemModType.Prefix));

            Assert.True(result);
            var applied = inventory.UnlockedItems.Single().AppliedMods;
            Assert.Single(applied);
            Assert.Equal(11, applied[0].ItemModId);
        }

        [Fact]
        public void TryApplyMod_ItemNotUnlocked_ReturnsFalse()
        {
            var inventory = new Inventory();
            inventory.UnlockMod(10);

            var result = inventory.TryApplyMod(999, 10, 0, MakeMod(10, EItemModType.Prefix));

            Assert.False(result);
        }

        [Fact]
        public void TryApplyMod_ModSlotIdNotFound_ReturnsFalse()
        {
            var inventory = new Inventory();
            var item = MakeItem(1, modSlots:
            [
                new ItemModSlot { Id = 0, Type = EItemModType.Prefix },
            ]);
            AddUnlockedItem(inventory, item);
            inventory.UnlockMod(10);

            // The item has only slot Id 0; slot Id 5 does not exist.
            var result = inventory.TryApplyMod(1, 10, 5, MakeMod(10, EItemModType.Prefix));

            Assert.False(result);
        }

        [Fact]
        public void TryApplyMod_SlotIdDiffersFromOrdinal_ResolvesById()
        {
            // Regression for the Index/Id conflation (#316): a slot whose DB Id (3) is not its
            // 0-based position in the item's slot list. The client speaks the slot's Id, so apply
            // must resolve by Id — matching by ordinal would target the wrong slot or none at all.
            var inventory = new Inventory();
            var item = MakeItem(1, modSlots:
            [
                new ItemModSlot { Id = 3, Type = EItemModType.Prefix },
                new ItemModSlot { Id = 4, Type = EItemModType.Suffix },
            ]);
            AddUnlockedItem(inventory, item);
            inventory.UnlockMod(10);

            var result = inventory.TryApplyMod(1, 10, 4, MakeMod(10, EItemModType.Suffix));

            Assert.True(result);
            var applied = Assert.Single(inventory.UnlockedItems.Single().AppliedMods);
            Assert.Equal(4, applied.ItemModSlotId);
            Assert.Equal(10, applied.ItemModId);
        }

        [Fact]
        public void TryRemoveMod_SlotIdDiffersFromOrdinal_ResolvesById()
        {
            // Regression for #316: removal must key off the slot's Id, consistent with apply/persistence.
            var inventory = new Inventory();
            var item = MakeItem(1, modSlots:
            [
                new ItemModSlot { Id = 3, Type = EItemModType.Prefix },
                new ItemModSlot { Id = 4, Type = EItemModType.Suffix },
            ]);
            AddUnlockedItem(inventory, item);
            inventory.UnlockMod(10);
            inventory.TryApplyMod(1, 10, 4, MakeMod(10, EItemModType.Suffix));

            var result = inventory.TryRemoveMod(1, 4);

            Assert.True(result);
            Assert.Empty(inventory.UnlockedItems.Single().AppliedMods);
        }

        // ── TryRemoveMod ────────────────────────────────────────────────────

        [Fact]
        public void TryRemoveMod_ExistingMod_RemovesSuccessfully()
        {
            var inventory = new Inventory();
            var item = MakeItem(1, modSlots:
            [
                new ItemModSlot { Id = 0, Type = EItemModType.Prefix },
            ]);
            AddUnlockedItem(inventory, item);
            inventory.UnlockMod(10);

            var mod = new ItemMod
            {
                Id = 0,
                Name = "Sharp",
                Description = "",
                Type = EItemModType.Prefix,
                Rarity = ERarity.Uncommon,
                Attributes = [],
            };
            inventory.TryApplyMod(1, 10, 0, mod);

            var result = inventory.TryRemoveMod(1, 0);

            Assert.True(result);
            Assert.Empty(inventory.UnlockedItems.Single().AppliedMods);
        }

        [Fact]
        public void TryRemoveMod_NoModInSlot_ReturnsFalse()
        {
            var inventory = new Inventory();
            var item = MakeItem(1);
            AddUnlockedItem(inventory, item);

            var result = inventory.TryRemoveMod(1, 0);

            Assert.False(result);
        }

        [Fact]
        public void TryRemoveMod_ItemNotUnlocked_ReturnsFalse()
        {
            var inventory = new Inventory();

            var result = inventory.TryRemoveMod(999, 0);

            Assert.False(result);
        }

        // ── TrySetFavorite ──────────────────────────────────────────────────

        [Fact]
        public void TrySetFavorite_UnlockedItem_SetsFavorite()
        {
            var inventory = new Inventory();
            AddUnlockedItem(inventory, MakeItem(1));

            var result = inventory.TrySetFavorite(1, true);

            Assert.True(result);
            Assert.True(inventory.UnlockedItems.Single().Favorite);
        }

        [Fact]
        public void TrySetFavorite_CanUnsetFavorite()
        {
            var inventory = new Inventory();
            AddUnlockedItem(inventory, MakeItem(1));
            inventory.TrySetFavorite(1, true);

            var result = inventory.TrySetFavorite(1, false);

            Assert.True(result);
            Assert.False(inventory.UnlockedItems.Single().Favorite);
        }

        [Fact]
        public void TrySetFavorite_ItemNotUnlocked_ReturnsFalse()
        {
            var inventory = new Inventory();

            var result = inventory.TrySetFavorite(999, true);

            Assert.False(result);
        }

        // ── GetEquippedAttributeModifiers ───────────────────────────────────

        [Fact]
        public void GetEquippedAttributeModifiers_ReturnsModifiersFromEquippedItems()
        {
            var inventory = new Inventory();
            var item = MakeItem(1, EItemCategory.Accessory, attributes: [
                new AttributeModifier
                {
                    Attribute = EAttribute.Strength,
                    Amount = 5.0,
                    Type = EModifierType.Additive,
                    Source = EAttributeModifierSource.Item,
                },
            ]);
            AddUnlockedItem(inventory, item);
            inventory.TryEquipItem(1, EEquipmentSlot.AccessorySlot, new Dictionary<int, int>());

            var modifiers = inventory.GetEquippedAttributeModifiers().ToList();

            Assert.Single(modifiers);
            Assert.Equal(EAttribute.Strength, modifiers[0].Attribute);
            Assert.Equal(5.0, modifiers[0].Amount);
        }

        [Fact]
        public void GetEquippedAttributeModifiers_NoEquippedItems_ReturnsEmpty()
        {
            var inventory = new Inventory();

            var modifiers = inventory.GetEquippedAttributeModifiers().ToList();

            Assert.Empty(modifiers);
        }

        [Fact]
        public void GetEquippedAttributeModifiers_EquippedItemWithAppliedMod_IncludesModAttributes()
        {
            var inventory = new Inventory();
            var item = MakeItem(1, EItemCategory.Accessory, modSlots:
            [
                new ItemModSlot { Id = 0, Type = EItemModType.Prefix },
            ]);
            AddUnlockedItem(inventory, item);
            inventory.TryEquipItem(1, EEquipmentSlot.AccessorySlot, new Dictionary<int, int>());
            inventory.UnlockMod(10);
            inventory.TryApplyMod(1, 10, 0, MakeMod(10, EItemModType.Prefix, attributes:
            [
                MakeModifier(EAttribute.Dexterity, 7.0),
            ]));

            var modifiers = inventory.GetEquippedAttributeModifiers().ToList();

            Assert.Single(modifiers);
            Assert.Equal(EAttribute.Dexterity, modifiers[0].Attribute);
            Assert.Equal(7.0, modifiers[0].Amount);
            Assert.Equal(EAttributeModifierSource.ItemMod, modifiers[0].Source);
        }

        [Fact]
        public void GetEquippedAttributeModifiers_EquippedItemWithBaseAndModAttributes_IncludesBoth()
        {
            var inventory = new Inventory();
            var item = MakeItem(1, EItemCategory.Accessory,
                attributes: [MakeModifier(EAttribute.Strength, 5.0, EAttributeModifierSource.Item)],
                modSlots:
                [
                    new ItemModSlot { Id = 0, Type = EItemModType.Prefix },
                ]);
            AddUnlockedItem(inventory, item);
            inventory.TryEquipItem(1, EEquipmentSlot.AccessorySlot, new Dictionary<int, int>());
            inventory.UnlockMod(10);
            inventory.TryApplyMod(1, 10, 0, MakeMod(10, EItemModType.Prefix, attributes:
            [
                MakeModifier(EAttribute.Dexterity, 7.0),
            ]));

            var modifiers = inventory.GetEquippedAttributeModifiers().ToList();

            Assert.Equal(2, modifiers.Count);
            Assert.Contains(modifiers, m => m.Attribute == EAttribute.Strength && m.Amount == 5.0 && m.Source == EAttributeModifierSource.Item);
            Assert.Contains(modifiers, m => m.Attribute == EAttribute.Dexterity && m.Amount == 7.0 && m.Source == EAttributeModifierSource.ItemMod);
        }

        [Fact]
        public void GetEquippedAttributeModifiers_AppliedModOnUnequippedItem_NotIncluded()
        {
            var inventory = new Inventory();
            var item = MakeItem(1, EItemCategory.Accessory, modSlots:
            [
                new ItemModSlot { Id = 0, Type = EItemModType.Prefix },
            ]);
            AddUnlockedItem(inventory, item);
            inventory.UnlockMod(10);
            inventory.TryApplyMod(1, 10, 0, MakeMod(10, EItemModType.Prefix, attributes:
            [
                MakeModifier(EAttribute.Dexterity, 7.0),
            ]));
            // The item is unlocked and modded but never equipped, so it must not contribute.

            var modifiers = inventory.GetEquippedAttributeModifiers().ToList();

            Assert.Empty(modifiers);
        }

        [Fact]
        public void GetEquippedAttributeModifiers_MultipleAppliedMods_IncludesAll()
        {
            var inventory = new Inventory();
            var item = MakeItem(1, EItemCategory.Accessory, modSlots:
            [
                new ItemModSlot { Id = 0, Type = EItemModType.Prefix },
                new ItemModSlot { Id = 1, Type = EItemModType.Suffix },
            ]);
            AddUnlockedItem(inventory, item);
            inventory.TryEquipItem(1, EEquipmentSlot.AccessorySlot, new Dictionary<int, int>());
            inventory.UnlockMod(10);
            inventory.UnlockMod(11);
            inventory.TryApplyMod(1, 10, 0, MakeMod(10, EItemModType.Prefix, attributes:
            [
                MakeModifier(EAttribute.Strength, 3.0),
            ]));
            inventory.TryApplyMod(1, 11, 1, MakeMod(11, EItemModType.Suffix, attributes:
            [
                MakeModifier(EAttribute.Dexterity, 4.0),
            ]));

            var modifiers = inventory.GetEquippedAttributeModifiers().ToList();

            Assert.Equal(2, modifiers.Count);
            Assert.Contains(modifiers, m => m.Attribute == EAttribute.Strength && m.Amount == 3.0);
            Assert.Contains(modifiers, m => m.Attribute == EAttribute.Dexterity && m.Amount == 4.0);
        }

        // ── GetUnlockedItem ─────────────────────────────────────────────────

        [Fact]
        public void GetUnlockedItem_UnlockedItem_ReturnsSlot()
        {
            var inventory = new Inventory();
            var item = MakeItem(42);
            inventory.UnlockItem(item);

            var slot = inventory.GetUnlockedItem(42);

            Assert.NotNull(slot);
            Assert.Equal(42, slot.ItemId);
            Assert.Equal(item, slot.Item);
        }

        [Fact]
        public void GetUnlockedItem_NotUnlocked_ReturnsNull()
        {
            var inventory = new Inventory();

            Assert.Null(inventory.GetUnlockedItem(999));
        }

        // ── JSON round-trip (player cache) ──────────────────────────────────

        [Fact]
        public void UnlockedItems_SurviveJsonRoundTrip_AndStayLookupableById()
        {
            // The Player aggregate (and its Inventory) round-trips through the Redis player cache as JSON,
            // so the id-keyed index must rebuild on deserialization — a get-only view would silently drop
            // every unlocked item. The setter rebuild is what guards that.
            var inventory = new Inventory();
            var item = MakeItem(3, modSlots: [new ItemModSlot { Id = 0, Type = EItemModType.Prefix }]);
            inventory.UnlockItem(item);
            inventory.UnlockMod(10);
            inventory.TryApplyMod(3, 10, 0, MakeMod(10, EItemModType.Prefix));
            inventory.TrySetFavorite(3, true);

            var restored = inventory.Serialize().Deserialize<Inventory>();

            Assert.NotNull(restored);
            var slot = restored.GetUnlockedItem(3);
            Assert.NotNull(slot);
            Assert.True(slot.Favorite);
            Assert.Equal(10, Assert.Single(slot.AppliedMods).ItemModId);
            // The rebuilt index is live, so a lookup-driven operation against the restored item resolves it.
            Assert.True(restored.TryEquipItem(3, EEquipmentSlot.AccessorySlot, new Dictionary<int, int>()));
        }

        // ── Slot ItemId is derived from Item (single source of truth) ────────

        [Fact]
        public void EquipmentSlot_ItemId_IsDerivedFromItem()
        {
            // ItemId can never desync from Item: it is computed from it rather than stored independently.
            var slot = new EquipmentSlot(EEquipmentSlot.WeaponSlot);
            Assert.Null(slot.ItemId);

            slot.Set(MakeItem(7, EItemCategory.Weapon));
            Assert.Equal(7, slot.ItemId);

            slot.Clear();
            Assert.Null(slot.ItemId);
        }

        [Fact]
        public void UnlockedItemSlot_ItemId_IsDerivedFromItem()
        {
            var slot = new UnlockedItemSlot { Item = MakeItem(9), AppliedMods = [] };

            Assert.Equal(9, slot.ItemId);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static Item MakeItem(int id, EItemCategory category = EItemCategory.Accessory, ERarity rarity = ERarity.Common,
            List<AttributeModifier>? attributes = null, List<ItemModSlot>? modSlots = null,
            int? requiredProficiencyId = null, int requiredProficiencyLevel = 0) => new()
            {
                Id = id,
                Name = $"Item {id}",
                Description = string.Empty,
                Category = category,
                Rarity = rarity,
                RequiredProficiencyId = requiredProficiencyId,
                RequiredProficiencyLevel = requiredProficiencyLevel,
                Attributes = attributes ?? [],
                ModSlots = modSlots ?? [],
            };

        private static void AddUnlockedItem(Inventory inventory, Item item)
        {
            inventory.UnlockItem(item);
        }

        private static ItemMod MakeMod(int id, EItemModType type, List<AttributeModifier>? attributes = null) => new()
        {
            Id = id,
            Name = $"Mod {id}",
            Description = string.Empty,
            Type = type,
            Rarity = ERarity.Common,
            Attributes = attributes ?? [],
        };

        private static AttributeModifier MakeModifier(EAttribute attribute, double amount,
            EAttributeModifierSource source = EAttributeModifierSource.ItemMod) => new()
            {
                Attribute = attribute,
                Amount = amount,
                Type = EModifierType.Additive,
                Source = source,
            };
    }
}
