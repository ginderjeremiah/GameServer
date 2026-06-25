import { describe, it, expect } from 'vitest';
import { chargeProjection, cooldownReadout } from '$routes/game/screens/fight/skill-cooldown';

describe('chargeProjection', () => {
	it('maps the charge fraction to a 0–360 sweep', () => {
		expect(chargeProjection(0, 1000)).toEqual({ sweep: 0, ready: false });
		expect(chargeProjection(500, 1000)).toEqual({ sweep: 180, ready: false });
	});

	it('is ready at (and past) full charge', () => {
		expect(chargeProjection(1000, 1000)).toEqual({ sweep: 360, ready: true });
		expect(chargeProjection(999, 1000).ready).toBe(true); // 99.9% threshold
		expect(chargeProjection(998, 1000).ready).toBe(false);
	});

	it('treats a zero cooldown as always ready instead of NaN', () => {
		// A zero-cooldown skill fires every tick; renderChargeTime / cooldownMs would be 0/0 = NaN.
		expect(chargeProjection(0, 0)).toEqual({ sweep: 360, ready: true });
	});
});

describe('cooldownReadout', () => {
	it('adjusts the cooldown by the recovery multiplier', () => {
		const r = cooldownReadout(2000, 0, 1);
		expect(r.adjustedCd).toBe(2);
		expect(r.remainingCd).toBe(2);
		expect(r.isReady).toBe(false);
		expect(r.progress).toBe(0);

		// A 2x recovery multiplier halves both the cooldown and the elapsed charge.
		const fast = cooldownReadout(2000, 1000, 2);
		expect(fast.adjustedCd).toBe(1);
		expect(fast.remainingCd).toBeCloseTo(0.5);
		expect(fast.progress).toBeCloseTo(50);
	});

	it('reports ready at full charge', () => {
		const r = cooldownReadout(2000, 2000, 1);
		expect(r.isReady).toBe(true);
		expect(r.progress).toBe(100);
	});

	it('treats a zero cooldown as ready at full fill instead of NaN', () => {
		const r = cooldownReadout(0, 0, 1);
		expect(r.adjustedCd).toBe(0);
		expect(r.isReady).toBe(true);
		expect(r.progress).toBe(100);
	});

	it('falls back to the raw cooldown when the recovery multiplier is non-positive', () => {
		const r = cooldownReadout(2000, 0, 0);
		expect(r.adjustedCd).toBe(2);
		expect(Number.isFinite(r.remainingCd)).toBe(true);
		expect(r.progress).toBe(0);
	});
});
