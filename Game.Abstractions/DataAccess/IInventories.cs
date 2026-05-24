using Game.Core.Players.Inventories;

namespace Game.Abstractions.DataAccess
{
    public interface IInventories
    {
        /// <summary>Persists a new inventory item for the player and returns its generated ID.</summary>
        public Task<int> AddInventoryItem(int playerId, int itemId, int slotNumber, int rating = 1);

        /// <summary>Applies a batch of slot reassignments to the player's persisted inventory.</summary>
        public Task UpdateInventoryItemSlots(int playerId, IEnumerable<IInventoryUpdate> updates);
    }
}
