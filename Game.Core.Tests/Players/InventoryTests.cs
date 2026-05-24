using Game.Core.Attributes.Modifiers;
using Game.Core.Items;
using Game.Core.Players;
using Game.Core.Players.Inventories;

namespace Game.Core.Tests.Players
{
    [TestClass]
    public class InventoryTests
    {
        // ── GetNextFreeSlotNumber ────────────────────────────────────────────

        [TestMethod]
        public void GetNextFreeSlotNumber_EmptyInventory_ReturnsZero()
        {
            var inventory = new Inventory();

            Assert.AreEqual(0, inventory.GetNextFreeSlotNumber());
        }

        [TestMethod]
        public void GetNextFreeSlotNumber_SkipsOccupiedSlots()
        {
            var inventory = new Inventory();
            inventory.TryAddItem(MakeItem(1), inventoryItemId: 10); // slot 0
            inventory.TryAddItem(MakeItem(2), inventoryItemId: 11); // slot 1

            Assert.AreEqual(2, inventory.GetNextFreeSlotNumber());
        }

        [TestMethod]
        public void GetNextFreeSlotNumber_WithGap_ReturnsLowestFree()
        {
            var inventory = new Inventory();
            // Manually place items at slots 0 and 2 (skip 1).
            inventory.InventorySlots.Add(new InventorySlot { InventoryItemId = 10, SlotNumber = 0, Item = MakeItem(1) });
            inventory.InventorySlots.Add(new InventorySlot { InventoryItemId = 11, SlotNumber = 2, Item = MakeItem(2) });

            Assert.AreEqual(1, inventory.GetNextFreeSlotNumber());
        }

        // ── TryAddItem ───────────────────────────────────────────────────────

        [TestMethod]
        public void TryAddItem_AddsItemToNextFreeSlot()
        {
            var inventory = new Inventory();
            var item = MakeItem(1);

            var slotNumber = inventory.TryAddItem(item, inventoryItemId: 99);

            Assert.AreEqual(0, slotNumber);
            Assert.AreEqual(1, inventory.InventorySlots.Count);
            Assert.AreEqual(item, inventory.InventorySlots[0].Item);
            Assert.AreEqual(99, inventory.InventorySlots[0].InventoryItemId);
        }

        [TestMethod]
        public void TryAddItem_MultipleItems_AssignsConsecutiveSlots()
        {
            var inventory = new Inventory();

            var slot0 = inventory.TryAddItem(MakeItem(1), inventoryItemId: 1);
            var slot1 = inventory.TryAddItem(MakeItem(2), inventoryItemId: 2);
            var slot2 = inventory.TryAddItem(MakeItem(3), inventoryItemId: 3);

            Assert.AreEqual(0, slot0);
            Assert.AreEqual(1, slot1);
            Assert.AreEqual(2, slot2);
        }

        // ── TryUpdateSlots ───────────────────────────────────────────────────

        [TestMethod]
        public void TryUpdateSlots_ValidReorder_ReturnsTrueAndApplies()
        {
            var inventory = new Inventory();
            var item1 = MakeItem(1);
            var item2 = MakeItem(2);
            inventory.TryAddItem(item1, inventoryItemId: 10); // slot 0
            inventory.TryAddItem(item2, inventoryItemId: 11); // slot 1

            // Swap them: item at id 10 → slot 1, item at id 11 → slot 0
            var updates = new List<SimpleInventoryUpdate>
            {
                new(Id: 10, SlotNumber: 1, Equipped: false),
                new(Id: 11, SlotNumber: 0, Equipped: false),
            };

            var result = inventory.TryUpdateSlots(updates);

            Assert.IsTrue(result);
            var slot0Item = inventory.InventorySlots.First(s => s.SlotNumber == 0).Item;
            var slot1Item = inventory.InventorySlots.First(s => s.SlotNumber == 1).Item;
            Assert.AreEqual(item2, slot0Item);
            Assert.AreEqual(item1, slot1Item);
        }

        [TestMethod]
        public void TryUpdateSlots_DuplicateDestinationSlot_ReturnsFalse()
        {
            var inventory = new Inventory();
            inventory.TryAddItem(MakeItem(1), inventoryItemId: 10);
            inventory.TryAddItem(MakeItem(2), inventoryItemId: 11);

            // Both items trying to go to slot 0
            var updates = new List<SimpleInventoryUpdate>
            {
                new(Id: 10, SlotNumber: 0, Equipped: false),
                new(Id: 11, SlotNumber: 0, Equipped: false),
            };

            Assert.IsFalse(inventory.TryUpdateSlots(updates));
        }

        [TestMethod]
        public void TryUpdateSlots_UnknownInventoryItemId_ReturnsFalse()
        {
            var inventory = new Inventory();
            inventory.TryAddItem(MakeItem(1), inventoryItemId: 10);

            var updates = new List<SimpleInventoryUpdate>
            {
                new(Id: 999, SlotNumber: 0, Equipped: false), // id 999 doesn't exist
            };

            Assert.IsFalse(inventory.TryUpdateSlots(updates));
        }

        [TestMethod]
        public void TryUpdateSlots_NegativeSlotNumber_ReturnsFalse()
        {
            var inventory = new Inventory();
            inventory.TryAddItem(MakeItem(1), inventoryItemId: 10);

            var updates = new List<SimpleInventoryUpdate>
            {
                new(Id: 10, SlotNumber: -1, Equipped: false),
            };

            Assert.IsFalse(inventory.TryUpdateSlots(updates));
        }

        // ── Equip / Unequip ──────────────────────────────────────────────────

        [TestMethod]
        public void TryUpdateSlots_MoveStorageToEquipped_EquipsItem()
        {
            var inventory = new Inventory();
            var item = MakeItem(1, EItemCategory.Accessory);
            inventory.TryAddItem(item, inventoryItemId: 10);

            var updates = new List<SimpleInventoryUpdate>
            {
                new(Id: 10, SlotNumber: (int)EEquipmentSlot.AccessorySlot, Equipped: true),
            };

            var result = inventory.TryUpdateSlots(updates);

            Assert.IsTrue(result);
            Assert.AreEqual(0, inventory.InventorySlots.Count);
            var equipped = inventory.EquipmentSlots.First(s => s.Value == EEquipmentSlot.AccessorySlot);
            Assert.AreEqual(item, equipped.Item);
            Assert.AreEqual(10, equipped.InventoryItemId);
        }

        [TestMethod]
        public void TryUpdateSlots_MoveEquippedToStorage_UnequipsItem()
        {
            var inventory = new Inventory();
            var item = MakeItem(1, EItemCategory.Accessory);
            inventory.TryAddItem(item, inventoryItemId: 10);

            // First equip it
            inventory.TryUpdateSlots([
                new SimpleInventoryUpdate(Id: 10, SlotNumber: (int)EEquipmentSlot.AccessorySlot, Equipped: true),
            ]);

            // Then move it back to storage
            var result = inventory.TryUpdateSlots([
                new SimpleInventoryUpdate(Id: 10, SlotNumber: 0, Equipped: false),
            ]);

            Assert.IsTrue(result);
            Assert.AreEqual(1, inventory.InventorySlots.Count);
            Assert.AreEqual(item, inventory.InventorySlots[0].Item);
            var slot = inventory.EquipmentSlots.First(s => s.Value == EEquipmentSlot.AccessorySlot);
            Assert.IsNull(slot.Item);
        }

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
            inventory.TryAddItem(item, inventoryItemId: 10);

            // Equip it
            inventory.TryUpdateSlots([
                new SimpleInventoryUpdate(Id: 10, SlotNumber: (int)EEquipmentSlot.AccessorySlot, Equipped: true),
            ]);

            var modifiers = inventory.GetEquippedAttributeModifiers().ToList();
            Assert.AreEqual(1, modifiers.Count);
            Assert.AreEqual(EAttribute.Strength, modifiers[0].Attribute);
            Assert.AreEqual(5.0, modifiers[0].Amount);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static Item MakeItem(int id) => MakeItem(id, EItemCategory.Accessory);

        private static Item MakeItem(int id, EItemCategory category, List<AttributeModifier>? attributes = null) => new()
        {
            Id = id,
            Name = $"Item {id}",
            Description = string.Empty,
            Category = category,
            Attributes = attributes ?? [],
            ModSlots = [],
            Tags = [],
        };

        /// <summary>Minimal <see cref="IInventoryUpdate"/> implementation for tests.</summary>
        private record SimpleInventoryUpdate(int Id, int SlotNumber, bool Equipped) : IInventoryUpdate;
    }
}
