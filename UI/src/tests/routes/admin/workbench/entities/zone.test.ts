import { describe, it, expect, beforeEach, vi } from 'vitest';

/* Zone config transforms: optional-FK normalization to/from the picker's "None"
   sentinel (-1), the spawn table derived from the enemies' embedded spawn lists,
   and the inverse mapping on persist. `fetchSocketData` (reference reads) and
   `ApiRequest` (the persistence writes) are stubbed; the real `persistEntity`
   orchestration runs unmocked so the transforms are tested in their real pipeline. */

const { staticData, socket, mockPost, mockFetch } = vi.hoisted(() => {
	const socket = { zones: [] as unknown[], enemies: [] as unknown[] };
	return {
		// eslint-disable-next-line @typescript-eslint/no-explicit-any
		staticData: {} as any,
		socket,
		mockPost: vi.fn(),
		mockFetch: vi.fn(async (command: string) => {
			switch (command) {
				case 'GetZones':
					return socket.zones;
				case 'GetEnemies':
					return socket.enemies;
				default:
					return [];
			}
		})
	};
});

vi.mock('$stores', () => ({ staticData }));
vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	class ApiRequest {
		static post = mockPost;
		static get = vi.fn();
	}
	return { ...actual, ApiRequest, fetchSocketData: mockFetch };
});

import { zoneEntity, type WorkbenchZone } from '$routes/admin/workbench/entities/zone';

/** Finds the body posted to a given AdminTools endpoint (or undefined if never called). */
const postBodyTo = (endpoint: string) => mockPost.mock.calls.find((c) => c[0] === endpoint)?.[1];

beforeEach(() => {
	mockPost.mockReset().mockResolvedValue(undefined);
	mockFetch.mockClear();
	socket.zones = [];
	socket.enemies = [];
	for (const key of Object.keys(staticData)) {
		delete staticData[key];
	}
});

describe('zoneEntity', () => {
	it('newItem seeds the optional FKs to the "None" sentinel (-1)', () => {
		const z = zoneEntity.newItem(99);
		expect(z).toMatchObject({ id: 99, bossEnemyId: -1, unlockChallengeId: -1, zoneEnemies: [] });
	});

	it("refresh normalizes absent FKs to -1 and derives each zone's spawn table", async () => {
		socket.zones = [
			{ id: 0, name: 'Hollow', description: '', order: 0, levelMin: 1, levelMax: 5, bossLevel: 1 },
			{
				id: 1,
				name: 'Cavern',
				description: '',
				order: 1,
				levelMin: 5,
				levelMax: 10,
				bossEnemyId: 7,
				bossLevel: 3,
				unlockChallengeId: 2
			}
		];
		socket.enemies = [
			{
				id: 10,
				name: 'Bat',
				isBoss: false,
				attributeDistribution: [],
				skillPool: [],
				spawns: [{ zoneId: 0, weight: 5 }]
			},
			{
				id: 7,
				name: 'Warden',
				isBoss: true,
				attributeDistribution: [],
				skillPool: [],
				spawns: [
					{ zoneId: 1, weight: 2 },
					{ zoneId: 0, weight: 1 }
				]
			}
		];

		const rows = await zoneEntity.refresh();

		// Absent boss/unlock FKs become the picker sentinel; present ones pass through.
		expect(rows[0]).toMatchObject({ bossEnemyId: -1, unlockChallengeId: -1 });
		expect(rows[1]).toMatchObject({ bossEnemyId: 7, unlockChallengeId: 2 });
		// The spawn table is rebuilt from the enemies' embedded spawn lists.
		expect(rows[0].zoneEnemies).toEqual([
			{ enemyId: 10, weight: 5 },
			{ enemyId: 7, weight: 1 }
		]);
		expect(rows[1].zoneEnemies).toEqual([{ enemyId: 7, weight: 2 }]);
		// And the catalogues it loaded are cached for the rest of the workbench.
		expect(staticData.zones).toBe(socket.zones);
		expect(staticData.enemies).toBe(socket.enemies);
	});

	it('persist maps the sentinel back to an absent FK, passes real ids through, and saves the spawn table against the resolved id', async () => {
		const newZone = (over: Partial<WorkbenchZone>): WorkbenchZone => ({
			id: -1,
			name: '',
			description: '',
			designerNotes: '',
			order: 0,
			levelMin: 1,
			levelMax: 10,
			bossEnemyId: -1,
			bossLevel: 1,
			unlockChallengeId: -1,
			isHome: false,
			zoneEnemies: [],
			...over
		});
		const added = [
			newZone({ id: -1, name: 'Open', zoneEnemies: [{ enemyId: 10, weight: 5 }] }), // both FKs absent
			newZone({ id: -2, name: 'Gated', bossEnemyId: 7, unlockChallengeId: 2 }) // both FKs set
		];
		// After the primary save the two adds receive ids 1 and 2 (send order → ascending ids).
		socket.zones = [
			{ id: 1, name: 'Open', description: '', order: 0, levelMin: 1, levelMax: 10, bossLevel: 1 },
			{
				id: 2,
				name: 'Gated',
				description: '',
				order: 0,
				levelMin: 1,
				levelMax: 10,
				bossEnemyId: 7,
				bossLevel: 1,
				unlockChallengeId: 2
			}
		];

		await zoneEntity.persist({ added, modified: [], deleted: [], existingIds: [] });

		const changes = postBodyTo('AdminTools/AddEditZones');
		expect(changes[0].item).toMatchObject({ name: 'Open', bossEnemyId: undefined, unlockChallengeId: undefined });
		expect(changes[1].item).toMatchObject({ name: 'Gated', bossEnemyId: 7, unlockChallengeId: 2 });
		// The first add (id -1) resolves to the first new id (1) for its spawn-table write.
		expect(postBodyTo('AdminTools/SetZoneEnemies')).toEqual({ zoneId: 1, zoneEnemies: [{ enemyId: 10, weight: 5 }] });
	});

	it('headline names the dedicated boss, or is blank without one', () => {
		// Enemies are indexed by id (Id-as-index), so the boss sits at enemies[7].
		const enemies: unknown[] = [];
		enemies[7] = { id: 7, name: 'Warden', isBoss: true };
		staticData.enemies = enemies;
		const gated = { ...zoneEntity.newItem(1), bossEnemyId: 7, bossLevel: 3 };
		expect(zoneEntity.headline?.(gated)).toBe('Boss: Warden · LV 3');
		expect(zoneEntity.headline?.({ ...gated, bossEnemyId: -1 })).toBe('');
	});

	it('meta surfaces the level band and spawn count', () => {
		const z = { ...zoneEntity.newItem(1), levelMin: 2, levelMax: 8, zoneEnemies: [{ enemyId: 1, weight: 1 }] };
		expect(zoneEntity.meta(z)).toEqual([
			['', 'L2–8'],
			['enemy', 1]
		]);
	});

	describe('zoneEnemies section warn/share (#2206)', () => {
		const zoneEnemiesSection = zoneEntity.sections.find((s) => s.key === 'zoneEnemies');
		const enemiesWarn = zoneEnemiesSection?.warn;
		const shareColumn =
			zoneEnemiesSection && 'columns' in zoneEnemiesSection
				? zoneEnemiesSection.columns.find((c) => c.key === '__share')
				: undefined;
		const shareTotal = shareColumn?.shareTotal;
		const shareValue = shareColumn?.shareValue;

		beforeEach(() => {
			staticData.enemies = [
				{ id: 0, name: 'Bat', isBoss: false, spawns: [] },
				{ id: 1, name: 'Warden', isBoss: true, retiredAt: '2026-01-01T00:00:00Z', spawns: [] }
			];
		});

		it('flags a zone whose only spawn rows belong to a retired enemy', () => {
			const z = { ...zoneEntity.newItem(1), zoneEnemies: [{ enemyId: 1, weight: 5 }] };
			expect(enemiesWarn?.(z)).toBe('No enemies spawn here');
		});

		it('passes once a live enemy also spawns there', () => {
			const z = {
				...zoneEntity.newItem(1),
				zoneEnemies: [
					{ enemyId: 1, weight: 5 },
					{ enemyId: 0, weight: 3 }
				]
			};
			expect(enemiesWarn?.(z)).toBeNull();
		});

		it('blocks Save when a Home zone carries spawn rows (#2276, backend-enforced)', () => {
			const z = { ...zoneEntity.newItem(1), isHome: true, zoneEnemies: [{ enemyId: 0, weight: 3 }] };
			expect(enemiesWarn?.(z)).toEqual({ message: 'The Home zone cannot have enemy spawns', blocking: true });
		});

		it('passes a Home zone with no spawn rows, without the "no enemies spawn here" nag (#2276)', () => {
			const z = { ...zoneEntity.newItem(1), isHome: true, zoneEnemies: [] };
			expect(enemiesWarn?.(z)).toBeNull();
		});

		it("blocks clearing spawns and flipping isHome in the same save — the backend reads the persisted count before this save's own SetZoneEnemies clears it (#2314)", () => {
			const baseline = { ...zoneEntity.newItem(1), isHome: false, zoneEnemies: [{ enemyId: 0, weight: 3 }] };
			const z = { ...baseline, isHome: true, zoneEnemies: [] };
			expect(enemiesWarn?.(z, baseline)).toEqual({
				message: 'Clear spawns in a separate save before making this zone Home',
				blocking: true
			});
		});

		it('passes flipping isHome on when the spawn table was already cleared in an earlier save (#2314)', () => {
			const baseline = { ...zoneEntity.newItem(1), isHome: false, zoneEnemies: [] };
			const z = { ...baseline, isHome: true };
			expect(enemiesWarn?.(z, baseline)).toBeNull();
		});

		it('passes a brand-new Home zone (no baseline) even though it has no spawns to worry about (#2314)', () => {
			const z = { ...zoneEntity.newItem(-1), isHome: true, zoneEnemies: [] };
			expect(enemiesWarn?.(z, undefined)).toBeNull();
		});

		it("share total excludes a retired sibling's weight from the denominator", () => {
			const rows = [
				{ enemyId: 1, weight: 5 },
				{ enemyId: 0, weight: 3 }
			];
			// Only the live row's weight (3) counts — the retired sibling's 5 is dropped.
			expect(shareTotal?.(rows[1], rows, undefined)).toBe(3);
		});

		it('share total falls back to 1 when every spawn row is retired', () => {
			const rows = [{ enemyId: 1, weight: 5 }];
			expect(shareTotal?.(rows[0], rows, undefined)).toBe(1);
		});

		it("share value reports 0 for a retired enemy's own row, so it can't render over 100% (#2214 nit)", () => {
			const rows = [
				{ enemyId: 1, weight: 5 },
				{ enemyId: 0, weight: 3 }
			];
			expect(shareValue?.(rows[0], rows, undefined)).toBe(0);
		});

		it("share value passes through a live enemy's own weight unchanged", () => {
			const rows = [
				{ enemyId: 1, weight: 5 },
				{ enemyId: 0, weight: 3 }
			];
			expect(shareValue?.(rows[1], rows, undefined)).toBe(3);
		});
	});

	describe('identity section warn (#1996)', () => {
		const identityWarn = zoneEntity.sections.find((s) => s.key === 'identity')?.warn;

		it('flags a dedicated boss that has since lost its boss flag, blocking Save (backend-enforced)', () => {
			const enemies: unknown[] = [];
			enemies[7] = { id: 7, name: 'Warden', isBoss: false };
			staticData.enemies = enemies;
			const z = { ...zoneEntity.newItem(1), bossEnemyId: 7 };
			expect(identityWarn?.(z)).toEqual({
				message: 'Dedicated boss is no longer flagged as a boss',
				blocking: true
			});
		});

		it('passes when the dedicated boss still has the boss flag', () => {
			const enemies: unknown[] = [];
			enemies[7] = { id: 7, name: 'Warden', isBoss: true };
			staticData.enemies = enemies;
			const z = { ...zoneEntity.newItem(1), bossEnemyId: 7 };
			expect(identityWarn?.(z)).toBeNull();
		});

		it('passes when no dedicated boss is assigned', () => {
			expect(identityWarn?.(zoneEntity.newItem(1))).toBeNull();
		});

		it('blocks Save when a Home zone carries a dedicated boss (#2276, backend-enforced)', () => {
			const enemies: unknown[] = [];
			enemies[7] = { id: 7, name: 'Warden', isBoss: true };
			staticData.enemies = enemies;
			const z = { ...zoneEntity.newItem(1), isHome: true, bossEnemyId: 7 };
			expect(identityWarn?.(z)).toEqual({ message: 'The Home zone cannot have a boss', blocking: true });
		});

		it('passes a Home zone with no dedicated boss (#2276)', () => {
			const z = { ...zoneEntity.newItem(1), isHome: true };
			expect(identityWarn?.(z)).toBeNull();
		});
	});
});
