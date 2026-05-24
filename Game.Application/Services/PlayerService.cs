using Game.Abstractions.DataAccess;
using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Players;
using Game.Core.Players.Inventories;

namespace Game.Application.Services
{
    public class PlayerService(IPlayerRepository playerRepo)
    {
        private readonly IPlayerRepository _playerRepo = playerRepo;

        public async Task<Player?> LoadPlayer(int playerId)
        {
            return await _playerRepo.GetPlayer(playerId);
        }

        public async Task<bool> TryUpdateAttributes(Player player, IEnumerable<IAttributeUpdate> updates)
        {
            var success = player.TryUpdateAttributes(updates);
            if (success)
                await _playerRepo.SavePlayer(player);
            return success;
        }

        public AttributeCollection GetPlayerAttributes(Player player)
        {
            return player.GetAttributes();
        }

        public IEnumerable<AttributeModifier> GetAllModifiers(Player player)
        {
            return player.GetAllModifiers();
        }

        /// <summary>
        /// Validates and applies a batch of inventory slot reassignments both in-memory and to
        /// the persistence layer.
        /// </summary>
        /// <returns><c>true</c> if the update was valid and applied; <c>false</c> if any
        /// validation rule was violated (inventory is unchanged in that case).</returns>
        public async Task<bool> UpdateInventorySlots(Player player, IEnumerable<IInventoryUpdate> updates)
        {
            var updateList = updates.ToList();
            if (!player.Inventory.TryUpdateSlots(updateList))
                return false;

            await _playerRepo.UpdateInventoryItemSlots(player.Id, updateList);
            return true;
        }
    }
}
