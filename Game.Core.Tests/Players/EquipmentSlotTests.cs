using Game.Core.Players.Inventories;
using Xunit;

namespace Game.Core.Tests.Players
{
    public class EquipmentSlotTests
    {
        // ── Constructor derives Value, Name, and ItemCategory ───────────────

        [Theory]
        [InlineData(EEquipmentSlot.HelmSlot, "Helm Slot", EItemCategory.Helm)]
        [InlineData(EEquipmentSlot.ChestSlot, "Chest Slot", EItemCategory.Chest)]
        [InlineData(EEquipmentSlot.LegSlot, "Leg Slot", EItemCategory.Leg)]
        [InlineData(EEquipmentSlot.BootSlot, "Boot Slot", EItemCategory.Boot)]
        [InlineData(EEquipmentSlot.WeaponSlot, "Weapon Slot", EItemCategory.Weapon)]
        [InlineData(EEquipmentSlot.AccessorySlot, "Accessory Slot", EItemCategory.Accessory)]
        public void Constructor_SetsValueHumanReadableNameAndCategory(
            EEquipmentSlot slot, string expectedName, EItemCategory expectedCategory)
        {
            var equipmentSlot = new EquipmentSlot(slot);

            Assert.Equal(slot, equipmentSlot.Value);
            Assert.Equal(expectedName, equipmentSlot.Name);
            Assert.Equal(expectedCategory, equipmentSlot.ItemCategory);
        }

        [Fact]
        public void Constructor_NewSlot_IsEmpty()
        {
            var equipmentSlot = new EquipmentSlot(EEquipmentSlot.WeaponSlot);

            Assert.Null(equipmentSlot.ItemId);
            Assert.Null(equipmentSlot.Item);
        }

        [Fact]
        public void Constructor_UnmappedSlot_Throws()
        {
            // Every defined EEquipmentSlot maps to a category; an out-of-range value exercises the
            // guard that prevents constructing a slot with no associated item category.
            Assert.Throws<ArgumentException>(() => new EquipmentSlot((EEquipmentSlot)99));
        }
    }
}
