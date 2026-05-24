using Game.Abstractions.DataAccess;
using Game.Core;
using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Items;
using Game.Core.Players;

namespace Game.Application.Services
{
    public class PlayerService(IPlayerRepository playerRepo, IItemMods itemMods)
    {
        private readonly IPlayerRepository _playerRepo = playerRepo;
        private readonly IItemMods _itemMods = itemMods;

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

        public async Task<bool> EquipItem(Player player, int itemId, EEquipmentSlot slot)
        {
            if (!player.Inventory.TryEquipItem(itemId, slot))
                return false;

            await _playerRepo.EquipItem(player.Id, itemId, (int)slot);
            await _playerRepo.SavePlayer(player);
            return true;
        }

        public async Task<bool> UnequipItem(Player player, EEquipmentSlot slot)
        {
            var equipSlot = player.Inventory.EquipmentSlots.FirstOrDefault(s => s.Value == slot);
            if (equipSlot?.ItemId is null)
                return false;

            var itemId = equipSlot.ItemId.Value;
            if (!player.Inventory.TryUnequipItem(slot))
                return false;

            await _playerRepo.UnequipItem(player.Id, itemId);
            await _playerRepo.SavePlayer(player);
            return true;
        }

        public async Task<bool> ApplyMod(Player player, int itemId, int itemModId, int itemModSlotId)
        {
            var modEntity = _itemMods.GetItemMod(itemModId);
            if (modEntity is null)
                return false;

            var mod = new ItemMod
            {
                Name = modEntity.Name,
                Removable = modEntity.Removable,
                Description = modEntity.Description ?? string.Empty,
                Type = (EItemModType)modEntity.ItemModTypeId,
                Attributes = [],
                Tags = [],
            };

            if (!player.Inventory.TryApplyMod(itemId, itemModId, itemModSlotId, mod))
                return false;

            await _playerRepo.ApplyMod(player.Id, itemId, itemModSlotId, itemModId);
            await _playerRepo.SavePlayer(player);
            return true;
        }

        public async Task<bool> RemoveMod(Player player, int itemId, int itemModSlotId)
        {
            if (!player.Inventory.TryRemoveMod(itemId, itemModSlotId))
                return false;

            await _playerRepo.RemoveMod(player.Id, itemId, itemModSlotId);
            await _playerRepo.SavePlayer(player);
            return true;
        }
    }
}
