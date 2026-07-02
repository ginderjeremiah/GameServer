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

        /// <summary>
        /// The fixed pie of proficiency XP a won battle pays out at difficulty multiplier 1 (spike #982
        /// decision 4). The total is constant per victory — scaled by the <c>DefeatRewards</c> difficulty
        /// curve and then split across the proficiencies represented in the fight — so diversifying a
        /// loadout dilutes each track rather than minting more total XP. Computed server-side at battle
        /// completion (never in the seeded simulation), so it is server-authoritative and not mirrored to
        /// the client. A strawman magnitude, tunable against the authored per-proficiency XP curves.
        /// </summary>
        public const double ProficiencyXpPerVictory = 10.0;

        /// <summary>
        /// The resist-training rate applied to the portion of a resist path's pre-mitigation exposure that
        /// still landed despite the player's own type-resistance — paired with
        /// <see cref="ResistMitigatedTrainingRate"/> so a resist path trains faster the more of its exposure the
        /// player's own resistance actually blocks (#1454), rather than the two being weighted equally. A
        /// strawman magnitude, tunable during balancing.
        /// </summary>
        public const double ResistUnmitigatedTrainingRate = 0.25;

        /// <summary>
        /// The resist-training rate applied to the portion of a resist path's pre-mitigation exposure the
        /// player's own type-resistance blocked — see <see cref="ResistUnmitigatedTrainingRate"/> for the paired
        /// rate on the portion that still landed. Full rate rewards the resistance investment the path
        /// represents.
        /// </summary>
        public const double ResistMitigatedTrainingRate = 1.0;
    }
}
