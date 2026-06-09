import { describe, it, expect, beforeEach, vi } from 'vitest';
import {
	EChallengeGoalComparison,
	EChallengeType,
	EEntityType,
	EItemModType,
	ERarity,
	EStatisticType,
	type IChallenge,
	type IChallengeType,
	type IEnemy,
	type IItemMod
} from '$lib/api';

/* The workbench entity configs (`entities/*.ts`) are mostly declarative, but each
   carries real transform logic the issue called out as untested: FK normalization
   to/from the picker's "None" sentinel, record↔DTO shaping (stripping child
   collections off the identity DTO), and derived display data (meta/headline).
   These exercise that logic directly.

   `fetchSocketData` (reference reads) and `ApiRequest` (the persistence writes)
   are stubbed; everything else — the real `reference` singleton and the real
   `persistEntity` orchestration — runs unmocked so the transforms are tested in
   their actual pipeline. The mod/enemy/challenge-helpers logic the configs lean
   on is covered by its own suites. */

const { staticData, socket, mockPost, mockGet, mockFetch } = vi.hoisted(() => {
	const socket = {
		zones: [] as unknown[],
		enemies: [] as unknown[],
		challenges: [] as unknown[],
		itemMods: [] as unknown[]
	};
	return {
		// eslint-disable-next-line @typescript-eslint/no-explicit-any
		staticData: {} as any,
		socket,
		mockPost: vi.fn(),
		mockGet: vi.fn(),
		mockFetch: vi.fn(async (command: string) => {
			switch (command) {
				case 'GetZones':
					return socket.zones;
				case 'GetEnemies':
					return socket.enemies;
				case 'GetChallenges':
					return socket.challenges;
				case 'GetItemMods':
					return socket.itemMods;
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
		static get = mockGet;
	}
	return { ...actual, ApiRequest, fetchSocketData: mockFetch };
});

import { zoneEntity, type WorkbenchZone } from '$routes/admin/workbench/entities/zone';
import { enemyEntity } from '$routes/admin/workbench/entities/enemy';
import { itemModEntity } from '$routes/admin/workbench/entities/item-mod';
import { challengeEntity } from '$routes/admin/workbench/entities/challenge';
import { reference } from '$routes/admin/workbench/reference.svelte';

/** Finds the body posted to a given AdminTools endpoint (or undefined if never called). */
const postBodyTo = (endpoint: string) => mockPost.mock.calls.find((c) => c[0] === endpoint)?.[1];

beforeEach(() => {
	mockPost.mockReset().mockResolvedValue(undefined);
	mockGet.mockReset().mockResolvedValue([]);
	mockFetch.mockClear();
	socket.zones = [];
	socket.enemies = [];
	socket.challenges = [];
	socket.itemMods = [];
	for (const key of Object.keys(staticData)) {
		delete staticData[key];
	}
	reference.challengeTypes = [];
});

/* ── Zones: optional-FK normalization + spawn-table derivation ─────────────── */

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
			order: 0,
			levelMin: 1,
			levelMax: 10,
			bossEnemyId: -1,
			bossLevel: 1,
			unlockChallengeId: -1,
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
		staticData.enemies = [{ id: 7, name: 'Warden', isBoss: true }];
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
});

/* ── Enemies: identity DTO strips child collections; only changed children save ─ */

describe('enemyEntity', () => {
	const baseline: IEnemy = {
		id: 0,
		name: 'Bat',
		isBoss: false,
		attributeDistribution: [{ attributeId: 0, baseAmount: 1, amountPerLevel: 0 }],
		skillPool: [1],
		spawns: [{ zoneId: 0, weight: 5 }]
	};

	it('newItem starts with empty child collections', () => {
		expect(enemyEntity.newItem(5)).toEqual({
			id: 5,
			name: '',
			isBoss: false,
			attributeDistribution: [],
			skillPool: [],
			spawns: []
		});
	});

	it('badges only bosses', () => {
		expect(enemyEntity.listBadge?.({ ...baseline, isBoss: true })).toBe('Boss');
		expect(enemyEntity.listBadge?.(baseline)).toBeNull();
	});

	it('persist strips the child collections off the identity DTO and saves only the changed child', async () => {
		const record: IEnemy = { ...baseline, name: 'Cave Bat', skillPool: [1, 2] }; // identity + skills changed
		socket.enemies = [record];

		await enemyEntity.persist({ added: [], modified: [{ record, baseline }], deleted: [], existingIds: [0] });

		// The identity Edit carries no child data — those go through their own endpoints.
		expect(postBodyTo('AdminTools/AddEditEnemies')[0].item).toEqual({
			id: 0,
			name: 'Cave Bat',
			isBoss: false,
			attributeDistribution: [],
			skillPool: [],
			spawns: []
		});
		// Only the skill pool actually changed, so only its endpoint is hit.
		expect(postBodyTo('AdminTools/SetEnemySkills')).toEqual({ enemyId: 0, skillIds: [1, 2] });
		expect(postBodyTo('AdminTools/SetEnemyAttributeDistributions')).toBeUndefined();
		expect(postBodyTo('AdminTools/SetEnemySpawns')).toBeUndefined();
	});
});

/* ── Item mods: child-only edit must not touch the identity endpoint ──────────── */

describe('itemModEntity', () => {
	it('newItem defaults to a Common Component with empty collections', () => {
		expect(itemModEntity.newItem(4)).toEqual({
			id: 4,
			name: '',
			description: '',
			itemModTypeId: EItemModType.Component,
			rarityId: ERarity.Common,
			attributes: [],
			tags: []
		});
	});

	it('meta shows the mod type, attribute and tag counts', () => {
		const mod: IItemMod = {
			...itemModEntity.newItem(1),
			itemModTypeId: EItemModType.Prefix,
			attributes: [{ attributeId: 0, amount: 1 }],
			tags: [1, 2]
		};
		expect(itemModEntity.meta(mod)).toEqual([
			['', 'Prefix'],
			['attr', 1],
			['tag', 2]
		]);
	});

	it('persist diffs attribute changes without sending an identity Edit when identity is unchanged', async () => {
		const baseline: IItemMod = {
			id: 0,
			name: 'Sharp',
			description: '',
			itemModTypeId: EItemModType.Prefix,
			rarityId: ERarity.Rare,
			attributes: [{ attributeId: 0, amount: 1 }],
			tags: [1]
		};
		const record: IItemMod = { ...baseline, attributes: [{ attributeId: 0, amount: 3 }] }; // only the bonus amount changed
		socket.itemMods = [record];

		await itemModEntity.persist({ added: [], modified: [{ record, baseline }], deleted: [], existingIds: [0] });

		// Identity is identical once attributes/tags are stripped → no Add/Edit call.
		expect(postBodyTo('AdminTools/AddEditItemMods')).toBeUndefined();
		// The attribute diff is sent as an Edit of the single changed bonus.
		expect(postBodyTo('AdminTools/AddEditItemModAttributes')).toMatchObject({
			id: 0,
			changes: [{ item: { attributeId: 0, amount: 3 } }]
		});
		// Tags were untouched, so their endpoint is skipped.
		expect(postBodyTo('AdminTools/SetTagsForItemMod')).toBeUndefined();
	});
});

/* ── Challenges: type-derived defaults, derived display data, identity DTO ────── */

describe('challengeEntity', () => {
	const TYPES: IChallengeType[] = [
		{
			id: EChallengeType.EnemiesKilled,
			name: 'Enemies Killed',
			goalComparison: EChallengeGoalComparison.AtLeast,
			statisticType: {
				id: EStatisticType.EnemiesKilled,
				entityType: EEntityType.Enemy,
				bossOnly: false,
				name: 'Enemies Killed'
			}
		}
	];

	beforeEach(() => {
		reference.challengeTypes = TYPES;
	});

	it('newItem derives the tracked statistic and entity dimension from the default type', () => {
		const c = challengeEntity.newItem(3);
		expect(c).toMatchObject({
			id: 3,
			name: '',
			challengeTypeId: EChallengeType.EnemiesKilled,
			statisticType: EStatisticType.EnemiesKilled,
			entityType: EEntityType.Enemy,
			progressGoal: 10
		});
	});

	it('lists the challenge type name as its badge, falling back to a dash', () => {
		const c = challengeEntity.newItem(1);
		expect(challengeEntity.listBadge?.(c)).toBe('Enemies Killed');
		expect(challengeEntity.listBadge?.({ ...c, challengeTypeId: 999 as EChallengeType })).toBe('—');
	});

	it('headline renders the player-facing objective sentence', () => {
		const c = { ...challengeEntity.newItem(1), progressGoal: 10 };
		expect(challengeEntity.headline?.(c)).toBe('Defeat 10 enemies');
	});

	it('meta reports goal, scope and reward count', () => {
		const base = { ...challengeEntity.newItem(1), progressGoal: 1500 };
		expect(challengeEntity.meta(base)).toEqual([
			['goal', '1,500'],
			['', 'global'],
			['reward', 0]
		]);
		const scoped = { ...base, targetEntityId: 2, rewardItemId: 5, rewardItemModId: 9 };
		expect(challengeEntity.meta(scoped)).toEqual([
			['goal', '1,500'],
			['', 'scoped'],
			['reward', 2]
		]);
	});

	it('persist sends the whole challenge as its identity DTO (no child collections)', async () => {
		const record: IChallenge = { ...challengeEntity.newItem(0), name: 'New' };
		const baseline: IChallenge = { ...record, name: 'Old' };
		socket.challenges = [record];

		await challengeEntity.persist({ added: [], modified: [{ record, baseline }], deleted: [], existingIds: [0] });

		expect(postBodyTo('AdminTools/AddEditChallenges')[0].item).toEqual(record);
	});
});
