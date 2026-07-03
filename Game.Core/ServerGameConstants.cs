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
        /// The base pie (<c>basePie</c>) each proficiency path independently claims against on a won battle:
        /// <c>XP_path = basePie × clamp(pathActivity ÷ totalAttributes)</c> (spike #1318). Power-normalization
        /// is the difficulty curve, so the banded <c>DefeatRewards</c> difficulty multiplier is deliberately
        /// <em>not</em> applied again. Claims across axes (offense/defense/proc) don't share a pie — they are
        /// independent, overlapping claims — so a multi-axis loadout mints <em>more</em> total proficiency XP
        /// rather than diluting each track. Computed server-side at battle completion (never in the seeded
        /// simulation), so it is server-authoritative and not mirrored to the client. A strawman magnitude,
        /// tunable against the authored per-proficiency XP curves.
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

        /// <summary>
        /// The reference Toughness the combat rating (<see cref="Battle.CombatRating"/>, #1531) prices offense
        /// against — nonzero so a Sunder debuff and DoT's real Toughness-bypass premium both price meaningfully
        /// (a 0 reference would make both inert). A strawman value pending the calibration report (#1533).
        /// </summary>
        public const double RefToughness = 50.0;

        /// <summary>
        /// The reference incoming damage-type mix the combat rating prices survivability's resistance term
        /// against — the eight resistance-bearing leaf types (the weapon leaves carry no resistance of their
        /// own; see <see cref="Attributes.DamageTypes"/>), weighted uniformly. Matchup tech (the right resist
        /// against a specific enemy) is deliberately not priced per-fight — the rating stays intrinsic (spike
        /// #1526, Decision 3).
        /// </summary>
        public static readonly IReadOnlyList<EDamageType> RefIncomingTypeMix =
        [
            EDamageType.Physical, EDamageType.Fire, EDamageType.Water, EDamageType.Earth,
            EDamageType.Wind, EDamageType.Bleed, EDamageType.Poison, EDamageType.Burn,
        ];

        /// <summary>
        /// The reference attacks/sec the combat rating assumes for a matched opponent — the incoming attack
        /// frequency the riposte term prices against (each parry's proc opportunity rate). A strawman value
        /// pending the calibration report (#1533).
        /// </summary>
        public const double RefAttackRate = 1.0;

        /// <summary>
        /// The reference incoming damage/sec the combat rating assumes for a matched opponent — prices the
        /// reflect term (<c>DamageReflection × RefDps</c>). A strawman value pending the calibration report
        /// (#1533).
        /// </summary>
        public const double RefDps = 10.0;

        /// <summary>
        /// The reference fight duration in seconds the combat rating uses for the regen credit
        /// (<c>regenRate × RefFightDuration</c>) and the effect-uptime ramp horizon
        /// (<c>min(duration, RefFightDuration / 2)</c>). ~30s per the spike (#1526).
        /// </summary>
        public const double RefFightDuration = 30.0;
    }
}
