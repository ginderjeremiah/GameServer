using Game.Abstractions.DataAccess;
using Game.Core;
using Game.Core.Players;

namespace Game.Application.Services
{
    public class PlayerService(
        IPlayerRepository playerRepo, IItemMods itemMods, IPlayerProgressRepository playerProgress)
    {
        private readonly IPlayerRepository _playerRepo = playerRepo;
        private readonly IItemMods _itemMods = itemMods;
        private readonly IPlayerProgressRepository _playerProgress = playerProgress;

        public async Task<Player?> LoadPlayer(int playerId, CancellationToken cancellationToken = default)
        {
            return await _playerRepo.GetPlayer(playerId, cancellationToken);
        }

        public async Task<bool> TryUpdateAttributes(Player player, IEnumerable<IAttributeUpdate> updates, CancellationToken cancellationToken = default)
        {
            return await SaveIf(player, player.TryUpdateAttributes(updates), cancellationToken);
        }

        public async Task<bool> EquipItem(Player player, int itemId, EEquipmentSlot slot, CancellationToken cancellationToken = default)
        {
            // The proficiency gate is evaluated against the player's current levels, which live on the
            // separate PlayerProgress aggregate; the domain rule itself stays in Inventory.TryEquipItem.
            var proficiencyLevels = await _playerProgress.GetProficiencies(player.Id, cancellationToken);
            var levelsByProficiency = proficiencyLevels.ToDictionary(p => p.ProficiencyId, p => p.Level);
            return await SaveIf(player, player.TryEquipItem(itemId, slot, levelsByProficiency), cancellationToken);
        }

        public async Task<bool> UnequipItem(Player player, EEquipmentSlot slot, CancellationToken cancellationToken = default)
        {
            return await SaveIf(player, player.TryUnequipItem(slot), cancellationToken);
        }

        public async Task<bool> SetFavorite(Player player, int itemId, bool favorite, CancellationToken cancellationToken = default)
        {
            return await SaveIf(player, player.TrySetFavorite(itemId, favorite), cancellationToken);
        }

        public async Task<bool> SetSelectedSkills(Player player, IReadOnlyList<int> orderedSkillIds, CancellationToken cancellationToken = default)
        {
            return await SaveIf(player, player.TrySetSelectedSkills(orderedSkillIds), cancellationToken);
        }

        public async Task SaveLogPreferences(Player player, IEnumerable<LogPreference> preferences, CancellationToken cancellationToken = default)
        {
            foreach (var preference in preferences)
            {
                player.UpdateLogPreference(preference.LogType, preference.Enabled);
            }

            await _playerRepo.SavePlayer(player, cancellationToken);
        }

        public async Task<bool> ApplyMod(Player player, int itemId, int itemModId, int itemModSlotId, CancellationToken cancellationToken = default)
        {
            if (!_itemMods.ValidateItemModId(itemModId))
            {
                return false;
            }

            var mod = _itemMods.GetItemMod(itemModId);
            return await SaveIf(player, player.TryApplyMod(itemId, itemModId, itemModSlotId, mod), cancellationToken);
        }

        public async Task<bool> RemoveMod(Player player, int itemId, int itemModSlotId, CancellationToken cancellationToken = default)
        {
            return await SaveIf(player, player.TryRemoveMod(itemId, itemModSlotId), cancellationToken);
        }

        // Persists the player only when the domain mutation succeeded, returning whether it did — centralizing
        // the guard/save/return boilerplate the anti-cheat mutation commands share. A rejected mutation
        // (false) skips the save so it leaves no state change, matching the all-or-nothing contract.
        private async Task<bool> SaveIf(Player player, bool mutated, CancellationToken cancellationToken)
        {
            if (mutated)
            {
                await _playerRepo.SavePlayer(player, cancellationToken);
            }

            return mutated;
        }
    }
}
