using Game.Abstractions.DataAccess;
using Game.Core;
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
            if (!player.TryUpdateAttributes(updates))
                return false;

            await _playerRepo.SavePlayer(player);
            return true;
        }

        public async Task<bool> EquipItem(Player player, int itemId, EEquipmentSlot slot)
        {
            if (!player.TryEquipItem(itemId, slot))
                return false;

            await _playerRepo.SavePlayer(player);
            return true;
        }

        public async Task<bool> UnequipItem(Player player, EEquipmentSlot slot)
        {
            if (!player.TryUnequipItem(slot))
                return false;

            await _playerRepo.SavePlayer(player);
            return true;
        }

        public async Task<bool> SetFavorite(Player player, int itemId, bool favorite)
        {
            if (!player.TrySetFavorite(itemId, favorite))
                return false;

            await _playerRepo.SavePlayer(player);
            return true;
        }

        public async Task SaveLogPreferences(Player player, IEnumerable<LogPreference> preferences)
        {
            foreach (var preference in preferences)
            {
                player.UpdateLogPreference(preference.LogType, preference.Enabled);
            }

            await _playerRepo.SavePlayer(player);
        }

        public async Task<bool> ApplyMod(Player player, int itemId, int itemModId, int itemModSlotId)
        {
            var modEntity = _itemMods.LookupItemMod(itemModId);
            if (modEntity is null)
                return false;

            var mod = new ItemMod
            {
                Id = itemModId,
                Name = modEntity.Name,
                Description = modEntity.Description ?? string.Empty,
                Type = (EItemModType)modEntity.ItemModTypeId,
                Rarity = (ERarity)modEntity.RarityId,
                Attributes = modEntity.ItemModAttributes
                    .Select(ima => new AttributeModifier
                    {
                        Attribute = (EAttribute)ima.AttributeId,
                        Amount = (double)ima.Amount,
                        Type = EModifierType.Additive,
                        Source = EAttributeModifierSource.ItemMod,
                    })
                    .ToList(),
                Tags = [],
            };

            if (!player.TryApplyMod(itemId, itemModId, itemModSlotId, mod))
                return false;

            await _playerRepo.SavePlayer(player);
            return true;
        }

        public async Task<bool> RemoveMod(Player player, int itemId, int itemModSlotId)
        {
            if (!player.TryRemoveMod(itemId, itemModSlotId))
                return false;

            await _playerRepo.SavePlayer(player);
            return true;
        }
    }
}
