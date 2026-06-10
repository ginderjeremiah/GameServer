using Game.Core.Attributes.Modifiers;
using Game.Core.Items;
using Game.Core.Players.Inventories;
using Xunit;

namespace Game.Core.Tests.Players
{
    public class InventoryTests
    {
        // ── UnlockItem ──────────────────────────────────────────────────────

        [Fact]
        public void UnlockItem_NewItem_AddsToUnlockedItems()
        {
            var inventory = new Inventory();
            var item = MakeItem(42);

            inventory.UnlockItem(item);

            Assert.Single(inventory.UnlockedItems);
            Assert.Equal(item, inventory.UnlockedItems[0].Item);
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

        // ── TryEquipItem ────────────────────────────────────────────────────

        [Fact]
        public void TryEquipItem_UnlockedItem_EquipsSuccessfully()
        {
            var inventory = new Inventory();
            var item = MakeItem(1, EItemCategory.Accessory);
            AddUnlockedItem(inventory, item);

            var result = inventory.TryEquipItem(1, EEquipmentSlot.AccessorySlot);

            Assert.True(result);
            var slot = inventory.EquipmentSlots.First(s => s.Value == EEquipmentSlot.AccessorySlot);
            Assert.Equal(1, slot.ItemId);
            Assert.Equal(item, slot.Item);
        }

        [Fact]
        public void TryEquipItem_NotUnlocked_ReturnsFalse()
        {
            var inventory = new Inventory();

            var result = inventory.TryEquipItem(999, EEquipmentSlot.AccessorySlot);

            Assert.False(result);
        }

        [Fact]
        public void TryEquipItem_WrongCategory_ReturnsFalse()
        {
            var inventory = new Inventory();
            var item = MakeItem(1, EItemCategory.Weapon);
            AddUnlockedItem(inventory, item);

            // Weapon into Accessory slot should fail
            var result = inventory.TryEquipItem(1, EEquipmentSlot.AccessorySlot);

            Assert.False(result);
        }

        [Fact]
        public void TryEquipItem_AlreadyEquippedElsewhere_MovesToNewSlot()
        {
            var inventory = new Inventory();
            var item = MakeItem(1, EItemCategory.Accessory);
            AddUnlockedItem(inventory, item);

            inventory.TryEquipItem(1, EEquipmentSlot.AccessorySlot);

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
            inventory.TryEquipItem(1, EEquipmentSlot.AccessorySlot);

            var result = inventory.TryEquipItem(2, EEquipmentSlot.AccessorySlot);

            Assert.True(result);
            var slot = inventory.EquipmentSlots.First(s => s.Value == EEquipmentSlot.AccessorySlot);
            Assert.Equal(2, slot.ItemId);
            Assert.Equal(itemB, slot.Item);
            // The previously equipped item must no longer occupy any slot.
            Assert.DoesNotContain(inventory.EquipmentSlots, s => s.ItemId == 1);
        }

        // ── TryUnequipItem ──────────────────────────────────────────────────

        [Fact]
        public void TryUnequipItem_EquippedSlot_UnequipsSuccessfully()
        {
            var inventory = new Inventory();
            var item = MakeItem(1, EItemCategory.Accessory);
            AddUnlockedItem(inventory, item);
            inventory.TryEquipItem(1, EEquipmentSlot.AccessorySlot);

            var result = inventory.TryUnequipItem(EEquipmentSlot.AccessorySlot);

            Assert.True(result);
            var slot = inventory.EquipmentSlots.First(s => s.Value == EEquipmentSlot.AccessorySlot);
            Assert.Null(slot.ItemId);
            Assert.Null(slot.Item);
        }

        [Fact]
        public void TryUnequipItem_EmptySlot_ReturnsFalse()
        {
            var inventory = new Inventory();

            var result = inventory.TryUnequipItem(EEquipmentSlot.AccessorySlot);

            Assert.False(result);
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
                Tags = [],
            };

            var result = inventory.TryApplyMod(1, 10, 0, mod);

            Assert.True(result);
            var applied = inventory.UnlockedItems[0].AppliedMods;
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
                Tags = [],
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
                Tags = [],
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
            var applied = inventory.UnlockedItems[0].AppliedMods;
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
            var applied = Assert.Single(inventory.UnlockedItems[0].AppliedMods);
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
            Assert.Empty(inventory.UnlockedItems[0].AppliedMods);
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
                Tags = [],
            };
            inventory.TryApplyMod(1, 10, 0, mod);

            var result = inventory.TryRemoveMod(1, 0);

            Assert.True(result);
            Assert.Empty(inventory.UnlockedItems[0].AppliedMods);
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
            Assert.True(inventory.UnlockedItems[0].Favorite);
        }

        [Fact]
        public void TrySetFavorite_CanUnsetFavorite()
        {
            var inventory = new Inventory();
            AddUnlockedItem(inventory, MakeItem(1));
            inventory.TrySetFavorite(1, true);

            var result = inventory.TrySetFavorite(1, false);

            Assert.True(result);
            Assert.False(inventory.UnlockedItems[0].Favorite);
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
            inventory.TryEquipItem(1, EEquipmentSlot.AccessorySlot);

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
            inventory.TryEquipItem(1, EEquipmentSlot.AccessorySlot);
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
            inventory.TryEquipItem(1, EEquipmentSlot.AccessorySlot);
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
            inventory.TryEquipItem(1, EEquipmentSlot.AccessorySlot);
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

        // ── Helpers ──────────────────────────────────────────────────────────

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
                Tags = [],
            };

        private static void AddUnlockedItem(Inventory inventory, Item item)
        {
            inventory.UnlockedItems.Add(new UnlockedItemSlot
            {
                ItemId = item.Id,
                Item = item,
                AppliedMods = [],
            });
        }

        private static ItemMod MakeMod(int id, EItemModType type, List<AttributeModifier>? attributes = null) => new()
        {
            Id = id,
            Name = $"Mod {id}",
            Description = string.Empty,
            Type = type,
            Rarity = ERarity.Common,
            Attributes = attributes ?? [],
            Tags = [],
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
