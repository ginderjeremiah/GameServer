using Game.Core.Players;
using Game.Core.Players.Inventories;

namespace Game.Abstractions.DataAccess
{
    /// <summary>
    /// Bounded-context repository for all player-scoped persistence: player data and inventory.
    /// </summary>
    public interface IPlayerRepository
    {
        // ----- Player -----

        /// <inheritdoc cref="IPlayers.GetPlayer"/>
        Task<Player?> GetPlayer(int playerId);

        /// <inheritdoc cref="IPlayers.SavePlayer"/>
        Task SavePlayer(Player player);

        // ----- Inventory -----

        /// <inheritdoc cref="IInventories.AddInventoryItem"/>
        Task<int> AddInventoryItem(int playerId, int itemId, int slotNumber, int rating = 1);

        /// <inheritdoc cref="IInventories.UpdateInventoryItemSlots"/>
        Task UpdateInventoryItemSlots(int playerId, IEnumerable<IInventoryUpdate> updates);
    }
}
