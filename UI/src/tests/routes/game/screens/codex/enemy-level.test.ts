import { describe, it, expect } from 'vitest';
import type { IEnemy, IZone } from '$lib/api';
import { levelRange, spawnShare, zoneTotalWeight } from '$routes/game/screens/codex/enemy-level';

const zones = [
	{ id: 0, name: 'Emberreach', order: 1, levelMin: 1, levelMax: 10, bossEnemyId: 2, bossLevel: 10 },
	{ id: 1, name: 'Ashfen Marsh', order: 2, levelMin: 11, levelMax: 22, bossLevel: 22 },
	{ id: 2, name: 'Sunken Causeway', order: 3, levelMin: 18, levelMax: 28, bossLevel: 28 }
] as IZone[];

const enemy = (over: Partial<IEnemy>): IEnemy =>
	({ id: 0, name: 'E', isBoss: false, attributeDistribution: [], skillPool: [], spawns: [], ...over }) as IEnemy;

describe('levelRange', () => {
	it('fixes a boss at its zone’s bossLevel (min === max)', () => {
		const boss = enemy({ id: 2, isBoss: true, spawns: [] });
		expect(levelRange(boss, zones)).toEqual({ min: 10, max: 10, fixed: true });
	});

	it('spans the min/max levels across a normal enemy’s spawn zones', () => {
		const e = enemy({
			spawns: [
				{ zoneId: 0, weight: 60 },
				{ zoneId: 2, weight: 20 }
			]
		});
		// zone 0 (1–10) and zone 2 (18–28) → 1–28
		expect(levelRange(e, zones)).toEqual({ min: 1, max: 28, fixed: false });
	});

	it('uses the single zone’s band for a one-zone spawn', () => {
		const e = enemy({ spawns: [{ zoneId: 1, weight: 40 }] });
		expect(levelRange(e, zones)).toEqual({ min: 11, max: 22, fixed: false });
	});

	it('degrades to level 1 for an enemy with no resolvable spawn zone', () => {
		expect(levelRange(enemy({ spawns: [] }), zones)).toEqual({ min: 1, max: 1, fixed: false });
	});

	it('degrades to level 1 for a boss with no owning zone', () => {
		expect(levelRange(enemy({ id: 9, isBoss: true }), zones)).toEqual({ min: 1, max: 1, fixed: true });
	});
});

describe('zoneTotalWeight', () => {
	it('sums every enemy’s spawn weight in a zone', () => {
		const enemies = [
			enemy({
				id: 0,
				spawns: [
					{ zoneId: 1, weight: 40 },
					{ zoneId: 2, weight: 18 }
				]
			}),
			enemy({ id: 1, spawns: [{ zoneId: 1, weight: 35 }] }),
			enemy({ id: 2, spawns: [{ zoneId: 2, weight: 5 }] })
		];
		expect(zoneTotalWeight(1, enemies)).toBe(75);
		expect(zoneTotalWeight(2, enemies)).toBe(23);
		expect(zoneTotalWeight(0, enemies)).toBe(0);
	});
});

describe('spawnShare', () => {
	it('returns the rounded percentage of the zone total', () => {
		expect(spawnShare(40, 75)).toBe(53);
		expect(spawnShare(60, 80)).toBe(75);
	});

	it('guards a zero total', () => {
		expect(spawnShare(10, 0)).toBe(0);
	});
});
