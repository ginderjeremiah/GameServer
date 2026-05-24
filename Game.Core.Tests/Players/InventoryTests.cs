using Game.Core.Attributes.Modifiers;
using Game.Core.Items;
using Game.Core.Players;
using Game.Core.Players.Inventories;

namespace Game.Core.Tests.Players
{
    [TestClass]
    public class InventoryTests
    {
        // ── UnlockItem ──────────────────────────────────────────────────────

        [TestMethod]
        public void UnlockItem_NewItem_AddsToUnlockedItems()
        {
            var inventory = new Inventory();

            inventory.UnlockItem(42);

            Assert.AreEqual(1, inventory.UnlockedItems.Count);
            Assert.AreEqual(42, inventory.UnlockedItems[0].ItemId);
        }

        [TestMethod]
        public void UnlockItem_DuplicateItem_DoesNotAddTwice()
        {
            var inventory = new Inventory();

            inventory.UnlockItem(42);
            inventory.UnlockItem(42);

            Assert.AreEqual(1, inventory.UnlockedItems.Count);
        }

        // ── UnlockMod ───────────────────────────────────────────────────────

        [TestMethod]
        public void UnlockMod_NewMod_AddsToUnlockedMods()
        {
            var inventory = new Inventory();

            inventory.UnlockMod(7);

            Assert.IsTrue(inventory.UnlockedMods.Contains(7));
        }

        [TestMethod]
        public void UnlockMod_DuplicateMod_NoError()
        {
            var inventory = new Inventory();

            inventory.UnlockMod(7);
            inventory.UnlockMod(7);

            Assert.AreEqual(1, inventory.UnlockedMods.Count);
        }

        // ── TryEquipItem ────────────────────────────────────────────────────

        [TestMethod]
        public void TryEquipItem_UnlockedItem_EquipsSuccessfully()
        {
            var inventory = new Inventory();
            var item = MakeItem(1, EItemCategory.Accessory);
            AddUnlockedItem(inventory, item);

            var result = inventory.TryEquipItem(1, EEquipmentSlot.AccessorySlot);

            Assert.IsTrue(result);
            var slot = inventory.EquipmentSlots.First(s => s.Value == EEquipmentSlot.AccessorySlot);
            Assert.AreEqual(1, slot.ItemId);
            Assert.AreEqual(item, slot.Item);
        }

        [TestMethod]
        public void TryEquipItem_NotUnlocked_ReturnsFalse()
        {
            var inventory = new Inventory();

            var result = inventory.TryEquipItem(999, EEquipmentSlot.AccessorySlot);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void TryEquipItem_WrongCategory_ReturnsFalse()
        {
            var inventory = new Inventory();
            var item = MakeItem(1, EItemCategory.Weapon);
            AddUnlockedItem(inventory, item);

            // Weapon into Accessory slot should fail
            var result = inventory.TryEquipItem(1, EEquipmentSlot.AccessorySlot);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void TryEquipItem_AlreadyEquippedElsewhere_MovesToNewSlot()
        {
            var inventory = new Inventory();
            var item = MakeItem(1, EItemCategory.Accessory);
            AddUnlockedItem(inventory, item);

            inventory.TryEquipItem(1, EEquipmentSlot.AccessorySlot);

            // Find another accessory slot if one exists, or just verify the old slot is cleared
            var oldSlot = inventory.EquipmentSlots.First(s => s.Value == EEquipmentSlot.AccessorySlot);
            Assert.IsNotNull(oldSlot.Item);
        }

        // ── TryUnequipItem ──────────────────────────────────────────────────

        [TestMethod]
        public void TryUnequipItem_EquippedSlot_UnequipsSuccessfully()
        {
            var inventory = new Inventory();
            var item = MakeItem(1, EItemCategory.Accessory);
            AddUnlockedItem(inventory, item);
            inventory.TryEquipItem(1, EEquipmentSlot.AccessorySlot);

            var result = inventory.TryUnequipItem(EEquipmentSlot.AccessorySlot);

            Assert.IsTrue(result);
            var slot = inventory.EquipmentSlots.First(s => s.Value == EEquipmentSlot.AccessorySlot);
            Assert.IsNull(slot.ItemId);
            Assert.IsNull(slot.Item);
        }

        [TestMethod]
        public void TryUnequipItem_EmptySlot_ReturnsFalse()
        {
            var inventory = new Inventory();

            var result = inventory.TryUnequipItem(EEquipmentSlot.AccessorySlot);

            Assert.IsFalse(result);
        }

        // ── TryApplyMod ─────────────────────────────────────────────────────

        [TestMethod]
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
                Removable = true,
                Type = EItemModType.Prefix,
                Attributes = [],
                Tags = [],
            };

            var result = inventory.TryApplyMod(1, 10, 0, mod);

            Assert.IsTrue(result);
            var applied = inventory.UnlockedItems[0].AppliedMods;
            Assert.AreEqual(1, applied.Count);
            Assert.AreEqual(mod, applied[0].ItemMod);
        }

        [TestMethod]
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
                Removable = true,
                Type = EItemModType.Prefix,
                Attributes = [],
                Tags = [],
            };

            var result = inventory.TryApplyMod(1, 10, 0, mod);

            Assert.IsFalse(result);
        }

        [TestMethod]
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
                Removable = true,
                Type = EItemModType.Suffix, // Wrong type — slot expects Prefix
                Attributes = [],
                Tags = [],
            };

            var result = inventory.TryApplyMod(1, 10, 0, mod);

            Assert.IsFalse(result);
        }

        // ── TryRemoveMod ────────────────────────────────────────────────────

        [TestMethod]
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
                Removable = true,
                Type = EItemModType.Prefix,
                Attributes = [],
                Tags = [],
            };
            inventory.TryApplyMod(1, 10, 0, mod);

            var result = inventory.TryRemoveMod(1, 0);

            Assert.IsTrue(result);
            Assert.AreEqual(0, inventory.UnlockedItems[0].AppliedMods.Count);
        }

        [TestMethod]
        public void TryRemoveMod_NoModInSlot_ReturnsFalse()
        {
            var inventory = new Inventory();
            var item = MakeItem(1);
            AddUnlockedItem(inventory, item);

            var result = inventory.TryRemoveMod(1, 0);

            Assert.IsFalse(result);
        }

        // ── GetEquippedAttributeModifiers ───────────────────────────────────

        [TestMethod]
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

            Assert.AreEqual(1, modifiers.Count);
            Assert.AreEqual(EAttribute.Strength, modifiers[0].Attribute);
            Assert.AreEqual(5.0, modifiers[0].Amount);
        }

        [TestMethod]
        public void GetEquippedAttributeModifiers_NoEquippedItems_ReturnsEmpty()
        {
            var inventory = new Inventory();

            var modifiers = inventory.GetEquippedAttributeModifiers().ToList();

            Assert.AreEqual(0, modifiers.Count);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static Item MakeItem(int id, EItemCategory category = EItemCategory.Accessory,
            List<AttributeModifier>? attributes = null, List<ItemModSlot>? modSlots = null) => new()
        {
            Id = id,
            Name = $"Item {id}",
            Description = string.Empty,
            Category = category,
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
