using Game.Core.Players;

namespace Game.Abstractions.DataAccess
{
    /// <summary>
    /// Bounded-context repository for all player-scoped persistence.
    /// </summary>
    public interface IPlayerRepository
    {
        Task<Player?> GetPlayer(int playerId);
        Task SavePlayer(Player player);

        // Unlocked items
        Task UnlockItem(int playerId, int itemId);
        Task EquipItem(int playerId, int itemId, int equipmentSlotId);
        Task UnequipItem(int playerId, int itemId);

        // Unlocked mods
        Task UnlockMod(int playerId, int itemModId);

        // Applied mods
        Task ApplyMod(int playerId, int itemId, int itemModSlotId, int itemModId);
        Task RemoveMod(int playerId, int itemId, int itemModSlotId);

        // Statistics
        Task<long> IncrementStatistic(int playerId, int statisticTypeId, int entityId, long amount);

        // Challenges
        Task UpdateChallengeProgress(int playerId, int challengeId, int progress);
        Task CompleteChallenge(int playerId, int challengeId);
    }
}
