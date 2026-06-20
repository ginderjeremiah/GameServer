import { describe, it, expect } from 'vitest';
import type { IZone } from '$lib/api';
import { isZoneUnlocked, navigableZones, nextZoneByOrder, zonesByOrder } from '$lib/common/zone-progression';

const zone = (id: number, order: number, unlockChallengeId?: number, retiredAt?: string): IZone => ({
	id,
	name: `Zone ${id}`,
	description: '',
	order,
	levelMin: 1,
	levelMax: 10,
	bossLevel: 1,
	unlockChallengeId,
	retiredAt
});

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

describe('nextZoneByOrder', () => {
	const zones = [zone(30, 3), zone(10, 1), zone(20, 2)];

	it('returns the next zone in authored order', () => {
		expect(nextZoneByOrder(zones, 10)?.id).toBe(20);
		expect(nextZoneByOrder(zones, 20)?.id).toBe(30);
	});

	it('returns undefined past the last zone', () => {
		expect(nextZoneByOrder(zones, 30)).toBeUndefined();
	});

	it('returns undefined for an unknown current zone', () => {
		expect(nextZoneByOrder(zones, 999)).toBeUndefined();
	});
});
