using Game.Core;

namespace Game.Abstractions.Contracts
{
    /// <summary>Read contract for a single mod slot on an item.</summary>
    public class ItemModSlot : IModel
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        public EItemModType ItemModSlotTypeId { get; set; }
    }
}
