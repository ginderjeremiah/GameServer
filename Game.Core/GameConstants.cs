namespace Game.Core
{
    /// <summary>
    /// Gameplay constants that must stay identical on both sides of the frontend/backend boundary.
    /// Marked <see cref="ClientMirroredAttribute"/> so the code generator
    /// (<c>Game.Api.CodeGen.ApiCodeGenerator</c>) emits each public constant into the TypeScript
    /// client (<c>UI/src/lib/api/types/game-constants.ts</c>), making this class the single source of
    /// truth and closing the drift gap where these values were hand-copied as magic numbers on both
    /// sides (#306). The values must never be hand-mirrored on the frontend — consume the generated
    /// constants instead.
    /// </summary>
    [ClientMirrored]
    public static class GameConstants
    {
        /// <summary>The fixed battle-simulation tick size in milliseconds. Must match on both sides for battle parity.</summary>
        public const int MsPerTick = 40;

        /// <summary>The default maximum simulated battle duration in milliseconds before a battle times out.</summary>
        public const int DefaultMaxBattleMs = MsPerTick * 10000;

        /// <summary>The maximum number of skills a player may equip in their battle loadout. Enemies bring the same cap into battle for player/enemy symmetry.</summary>
        public const int MaxSelectedSkills = 4;

        /// <summary>The experience required to advance a level scales linearly as <c>Level * <see cref="ExpPerLevel"/></c>.</summary>
        public const int ExpPerLevel = 100;

        /// <summary>The number of stat points awarded on each level-up.</summary>
        public const int StatPointsPerLevel = 6;

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
