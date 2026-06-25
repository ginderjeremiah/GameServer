namespace Game.Core.Players
{
    /// <summary>
    /// A starter item a freshly created player begins with equipped: the item to unlock and the slot it
    /// occupies. Part of the <see cref="NewPlayer"/> blueprint — the weapon among these carries the class's
    /// signature skill via <c>Item.GrantedSkillId</c>, which comes online at creation through the normal
    /// equip path.
    /// </summary>
    public class NewPlayerEquipment
    {
        public required int ItemId { get; init; }

        public required EEquipmentSlot EquipmentSlot { get; init; }
    }
}
