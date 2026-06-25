/* Pure cooldown/charge display math for the fight screen's skill surfaces (the slot overlay and the
   skill tooltip's cooldown pill). A zero (or non-positive) cooldown skill fires every tick — in the
   battle, `chargeTime >= cooldownMs` is always true — so it reads as permanently ready here. The
   guards below keep that case from producing NaN/Infinity in the UI. */

/** Radial charge projection for a skill slot's cooldown overlay: `sweep` is the revealed arc in
 *  degrees (0–360, fed to the conic-gradient wedge) and `ready` thresholds the charge fraction at
 *  99.9%. A non-positive cooldown is fully charged (always ready, full sweep). */
export function chargeProjection(renderChargeTime: number, cooldownMs: number): { sweep: number; ready: boolean } {
	const fraction = cooldownMs > 0 ? renderChargeTime / cooldownMs : 1;
	return { sweep: fraction * 360, ready: fraction >= 0.999 };
}

/** Cooldown readout for the skill tooltip's cooldown pill: the player-adjusted cooldown and remaining
 *  time (seconds), readiness, and the 0–100 fill. A non-positive adjusted cooldown reads as ready at
 *  full fill. Mirrors the loadout screen's `cdMultiplier > 0` guard so a non-positive recovery
 *  multiplier falls back to the raw cooldown rather than dividing by zero. */
export function cooldownReadout(
	cooldownMs: number,
	renderChargeTime: number,
	cdMultiplier: number
): { adjustedCd: number; remainingCd: number; isReady: boolean; progress: number } {
	const adjustedCd = cdMultiplier > 0 ? cooldownMs / 1000 / cdMultiplier : cooldownMs / 1000;
	const charged = cdMultiplier > 0 ? renderChargeTime / 1000 / cdMultiplier : renderChargeTime / 1000;
	const remainingCd = Math.max(adjustedCd - charged, 0);
	const isReady = adjustedCd <= 0 || remainingCd <= 0.01;
	const progress = isReady ? 100 : Math.max(0, ((adjustedCd - remainingCd) / adjustedCd) * 100);
	return { adjustedCd, remainingCd, isReady, progress };
}
