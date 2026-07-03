using Game.Abstractions.Contracts;

namespace Game.Api.Models.Attributes
{
    /// <summary>
    /// The authoritative post-command stat state: the full allocation list plus the player's total
    /// spent points, so the client adopts <see cref="StatPointsUsed"/> absolutely (mirroring the
    /// <see cref="Enemies.DefeatRewards"/> reconcile) instead of re-deriving the spend locally.
    /// </summary>
    public class UpdatePlayerStatsResponse : IModel
    {
        public required List<BattlerAttribute> Attributes { get; set; }
        public required int StatPointsUsed { get; set; }
    }
}
