namespace Game.Core.Battle
{
    /// <summary>
    /// The read-only view of a <see cref="Battler"/>'s effect state that the overlay training signals consume
    /// (#2380) — the backend-only Hex/Sunder/Momentum share claims and the applied-contribution reads behind
    /// them. It is what <see cref="Battler.Effects"/> exposes, so applying or expiring an effect is reachable
    /// only through <see cref="Battler.ApplyEffect"/>/<see cref="Battler.AdvanceEffects"/>, which own the
    /// MaxHealth re-clamp a raw <see cref="BattlerEffects.Apply"/> would skip. Nothing here mutates.
    /// </summary>
    public interface IBattlerEffectTallies
    {
        /// <inheritdoc cref="BattlerEffects.HexBonusForHit"/>
        double HexBonusForHit(double bookedNet, EDamageType damageType);

        /// <inheritdoc cref="BattlerEffects.AppliedVulnerability"/>
        double AppliedVulnerability(EDamageType damageType);

        /// <inheritdoc cref="BattlerEffects.SunderBonusForHit"/>
        double SunderBonusForHit(double bookedNet);

        /// <inheritdoc cref="BattlerEffects.AppliedSunder"/>
        double AppliedSunder();

        /// <inheritdoc cref="BattlerEffects.AppliedMomentum"/>
        double AppliedMomentum(EDamageType damageType);
    }
}
