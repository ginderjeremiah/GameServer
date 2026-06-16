namespace Game.Core
{
    /// <summary>
    /// Server-authoritative gameplay constants that are deliberately NOT mirrored to the frontend.
    /// Unlike <see cref="GameConstants"/> — whose values must stay identical on both sides of the
    /// frontend/backend boundary — these back anti-cheat/reward clamps the client never evaluates (the
    /// server is the sole authority), so the class carries no <see cref="ClientMirroredAttribute"/> and
    /// its values never reach the generated TypeScript client.
    /// </summary>
    public static class ServerGameConstants
    {
        /// <summary>
        /// Upper clamp on the exp-reward difficulty multiplier (the quadratic <c>ratio²</c> in
        /// <c>DefeatRewards</c>). The multiplier saturates here so an enemy far above the player's power
        /// can't mint an unbounded single-battle payout. A cap of 4 saturates at a power ratio of 2× —
        /// an enemy twice the player's power already pays the maximum bonus, and ratios beyond that
        /// plateau instead of scaling quadratically without bound.
        /// </summary>
        public const double MaxExpRewardMultiplier = 4.0;

        /// <summary>
        /// Defensive ceiling on the experience a single <c>Player.GrantExp</c> call applies. Legitimate
        /// per-battle exp is already clamped well below this by <see cref="MaxExpRewardMultiplier"/>; this
        /// is a backstop that bounds the level-up loop (and its per-level event burst) against a
        /// tampered/replayed grant, keeping a single command's work on the serialized per-player path
        /// finite regardless of the caller.
        /// </summary>
        public const int MaxExpPerGrant = 100_000;
    }
}
