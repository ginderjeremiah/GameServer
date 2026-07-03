using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Players;
using Game.Core.Players.Inventories;

namespace Game.Core.TestInfrastructure.Builders
{
    /// <summary>
    /// Test-only attribute composition shortcut for a live <see cref="Player"/> aggregate, mirroring
    /// <see cref="BattlerFactory"/>'s rationale: production never composes attributes this way — every
    /// production battler is reconstructed from a frozen <see cref="Battle.BattleSnapshot"/>
    /// (<see cref="Battle.BattleSnapshot.GetModifiers"/>), which additionally layers the class locked base,
    /// proficiency bonuses, and signature passive that this shortcut omits. Used only by
    /// <see cref="BattlerFactory"/> and unit tests that need a battler/attribute set straight off stat
    /// allocations + equipped gear with no other setup.
    /// </summary>
    public static class PlayerAttributeTestExtensions
    {
        public static IEnumerable<AttributeModifier> GetAllModifiers(this Player player)
        {
            return player.StatPoints.ToAttributeModifiers()
                .Concat(player.Inventory.GetEquippedAttributeModifiers());
        }

        public static AttributeCollection GetAttributes(this Player player)
        {
            return new AttributeCollection(player.GetAllModifiers());
        }

        public static IEnumerable<AttributeModifier> GetEquippedAttributeModifiers(this Inventory inventory)
        {
            return inventory.EquipmentSlots.SelectNotNull(slot => slot.Item)
                .SelectMany(item => item.GetAttributeModifiers(GetAppliedMods(inventory, item.Id)));
        }

        private static IEnumerable<Items.ItemMod> GetAppliedMods(Inventory inventory, int itemId)
        {
            return inventory.GetUnlockedItem(itemId)?.AppliedMods.Select(applied => applied.ItemMod) ?? [];
        }
    }
}
