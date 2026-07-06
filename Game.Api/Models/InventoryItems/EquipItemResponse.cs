namespace Game.Api.Models.InventoryItems
{
    /// <summary>
    /// The authoritative post-change player state for an equip/unequip mutation: the player's
    /// post-change combat-rating (spike #1526 Decision 7), computed via
    /// <see cref="Game.Application.Services.BattleService.RatePlayer"/>. Shared by
    /// <see cref="Game.Api.Sockets.Commands.EquipItem"/> and
    /// <see cref="Game.Api.Sockets.Commands.UnequipItem"/> since gear is one of the three things
    /// Decision 7 calls out as moving the rating, mirroring the pattern
    /// <see cref="Game.Api.Models.Attributes.UpdatePlayerStatsResponse"/> established for stat reallocation.
    /// </summary>
    public class EquipItemResponse : IModel
    {
        public required double PlayerRating { get; set; }
    }
}
