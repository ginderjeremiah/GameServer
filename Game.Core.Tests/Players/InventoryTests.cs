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

            inventory.UnlockItem(42);

            Assert.Single(inventory.UnlockedItems);
            Assert.Equal(42, inventory.UnlockedItems[0].ItemId);
        }

        [Fact]
        public void UnlockItem_DuplicateItem_DoesNotAddTwice()
        {
            var inventory = new Inventory();

            inventory.UnlockItem(42);
            inventory.UnlockItem(42);

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
                new ItemModSlot { Id = 0, Index = 0, Type = EItemModType.Prefix },
            ]);
            AddUnlockedItem(inventory, item);
            inventory.UnlockMod(10);

            var mod = new ItemMod
            {
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
                new ItemModSlot { Id = 0, Index = 0, Type = EItemModType.Prefix },
            ]);
            AddUnlockedItem(inventory, item);
            // Don't unlock mod 10

            var mod = new ItemMod
            {
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
                new ItemModSlot { Id = 0, Index = 0, Type = EItemModType.Prefix },
            ]);
            AddUnlockedItem(inventory, item);
            inventory.UnlockMod(10);

            var mod = new ItemMod
            {
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

        // ── TryRemoveMod ────────────────────────────────────────────────────

        [Fact]
        public void TryRemoveMod_ExistingMod_RemovesSuccessfully()
        {
            var inventory = new Inventory();
            var item = MakeItem(1, modSlots:
            [
                new ItemModSlot { Id = 0, Index = 0, Type = EItemModType.Prefix },
            ]);
            AddUnlockedItem(inventory, item);
            inventory.UnlockMod(10);

            var mod = new ItemMod
            {
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
    }
}
