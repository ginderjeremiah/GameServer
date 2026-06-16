using Game.Core.Players.Inventories;
using Xunit;

namespace Game.Core.Tests.Players
{
    public class EquipmentSlotTests
    {
        // ── Name and ItemCategory are computed get-only from Value ──────────

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
        public void NameAndCategory_TrackValue_NeverGoStale()
        {
            // Name/ItemCategory are derived from Value rather than stored, so reassigning Value (e.g. a
            // deserializer or a future caller) can never leave a stale category that would mis-gate slot
            // compatibility.
            var equipmentSlot = new EquipmentSlot(EEquipmentSlot.WeaponSlot)
            {
                Value = EEquipmentSlot.HelmSlot,
            };

            Assert.Equal("Helm Slot", equipmentSlot.Name);
            Assert.Equal(EItemCategory.Helm, equipmentSlot.ItemCategory);
        }

        [Fact]
        public void SerializationRoundTrip_DerivesCategoryFromValue()
        {
            // The player aggregate round-trips through Redis as JSON; the derived category must follow Value
            // back out, not depend on a persisted Name/ItemCategory field.
            var roundTripped = new EquipmentSlot(EEquipmentSlot.WeaponSlot).Serialize().Deserialize<EquipmentSlot>();

            Assert.NotNull(roundTripped);
            Assert.Equal(EEquipmentSlot.WeaponSlot, roundTripped.Value);
            Assert.Equal(EItemCategory.Weapon, roundTripped.ItemCategory);
        }

        [Fact]
        public void ItemCategory_UnmappedSlot_Throws()
        {
            // Every defined EEquipmentSlot maps to a category; deriving ItemCategory from an out-of-range
            // value exercises the guard that prevents resolving a slot with no associated item category.
            var equipmentSlot = new EquipmentSlot((EEquipmentSlot)99);

            Assert.Throws<ArgumentException>(() => _ = equipmentSlot.ItemCategory);
        }
    }
}
