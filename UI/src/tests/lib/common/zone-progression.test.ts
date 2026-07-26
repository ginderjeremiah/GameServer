import { describe, it, expect } from 'vitest';
import type { IZone } from '$lib/api';
import { isZoneUnlocked, navigableZones, nextNavigableZone, zonesByOrder } from '$lib/common/zone-progression';
import { makeZone } from '../../fixtures/zones';

const zone = (id: number, order: number, unlockChallengeId?: number, retiredAt?: string): IZone =>
	makeZone({ id, order, unlockChallengeId, retiredAt });

describe('zonesByOrder', () => {
	it('sorts by authored order without mutating the input', () => {
		const input = [zone(30, 3), zone(10, 1), zone(20, 2)];
		const sorted = zonesByOrder(input);

		expect(sorted.map((z) => z.id)).toEqual([10, 20, 30]);
		// Defensive copy: the original array is untouched.
		expect(input.map((z) => z.id)).toEqual([30, 10, 20]);
	});
});

describe('navigableZones', () => {
	it('orders by authored order and excludes retired zones (skipped, not walls)', () => {
		const input = [zone(30, 3), zone(10, 1), zone(20, 2, undefined, '2026-01-01T00:00:00Z')];
		const result = navigableZones(input);

		// The retired middle zone (id 20) is dropped, so 10 and 30 stay reachable across it.
		expect(result.map((z) => z.id)).toEqual([10, 30]);
		// Defensive copy: the original array is untouched.
		expect(input.map((z) => z.id)).toEqual([30, 10, 20]);
	});

	it('keeps all zones when none are retired', () => {
		expect(navigableZones([zone(20, 2), zone(10, 1)]).map((z) => z.id)).toEqual([10, 20]);
	});
});

describe('isZoneUnlocked', () => {
	const never = () => false;
	const always = () => true;

	it('treats an ungated zone as always unlocked', () => {
		expect(isZoneUnlocked(zone(1, 1), never)).toBe(true);
	});

	it('locks a gated zone until its gating challenge is completed', () => {
		const gated = zone(2, 2, 7);
		expect(isZoneUnlocked(gated, never)).toBe(false);
		expect(isZoneUnlocked(gated, (id) => id === 7)).toBe(true);
		expect(isZoneUnlocked(gated, always)).toBe(true);
	});
});

describe('nextNavigableZone', () => {
	const zones = [zone(30, 3), zone(10, 1), zone(20, 2)];

	it('returns the next zone in authored order', () => {
		expect(nextNavigableZone(zones, 10)?.id).toBe(20);
		expect(nextNavigableZone(zones, 20)?.id).toBe(30);
	});

	it('returns undefined past the last zone', () => {
		expect(nextNavigableZone(zones, 30)).toBeUndefined();
	});

	it('returns undefined for an unknown current zone', () => {
		expect(nextNavigableZone(zones, 999)).toBeUndefined();
	});

	it('skips a retired zone rather than returning it as the next zone', () => {
		const withRetiredMiddle = [zone(30, 3), zone(10, 1), zone(20, 2, undefined, '2026-01-01T00:00:00Z')];

		expect(nextNavigableZone(withRetiredMiddle, 10)?.id).toBe(30);
	});

	it('returns undefined when the current zone is itself retired', () => {
		// A player is lazily relocated out of a retired zone, so it has no "next" to advance into.
		const retiredCurrent = [zone(10, 1, undefined, '2026-01-01T00:00:00Z'), zone(20, 2)];

		expect(nextNavigableZone(retiredCurrent, 10)).toBeUndefined();
	});
});
