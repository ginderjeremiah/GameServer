import { describe, it, expect, beforeEach, vi } from 'vitest';
import { EChangeType } from '$lib/api';
import type { TableSectionConfig } from '$routes/admin/workbench/entities/types';

/* Enemy config transforms: `newItem` defaults, the boss list badge, and the
   persist path — the identity DTO strips the child collections, and only the
   child collection that actually changed is saved. `fetchSocketData`/`ApiRequest`
   are stubbed; the real `persistEntity` orchestration runs unmocked. */

const { staticData, socket, mockPost, mockFetch } = vi.hoisted(() => {
	const socket = { enemies: [] as unknown[], zones: [] as unknown[] };
	return {
		// eslint-disable-next-line @typescript-eslint/no-explicit-any
		staticData: {} as any,
		socket,
		mockPost: vi.fn(),
		mockFetch: vi.fn(async (command: string) => {
			switch (command) {
				case 'GetEnemies':
					return socket.enemies;
				case 'GetZones':
					return socket.zones;
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

import { enemyEntity, type WorkbenchEnemy } from '$routes/admin/workbench/entities/enemy';

/** Finds the body posted to a given AdminTools endpoint (or undefined if never called). */
const postBodyTo = (endpoint: string) => mockPost.mock.calls.find((c) => c[0] === endpoint)?.[1];

/** A table section's config by key (for exercising its `newRow` factory). */
const tableSection = (key: string) =>
	enemyEntity.sections.find((s) => s.key === key) as TableSectionConfig<WorkbenchEnemy>;

/** The spawns section, used to exercise its boss-aware `warn` predicate. */
const spawnsWarn = () => enemyEntity.sections.find((s) => s.key === 'spawns')?.warn;

beforeEach(() => {
	mockPost.mockReset().mockResolvedValue(undefined);
	mockFetch.mockClear();
	socket.enemies = [];
	socket.zones = [];
	for (const key of Object.keys(staticData)) {
		delete staticData[key];
	}
});

describe('enemyEntity', () => {
	const baseline: WorkbenchEnemy = {
		id: 0,
		name: 'Bat',
		designerNotes: '',
		isBoss: false,
		attributeDistribution: [{ attributeId: 0, baseAmount: 1, amountPerLevel: 0 }],
		skillPool: [1],
		spawns: [{ zoneId: 0, weight: 5 }],
		bossZones: []
	};

	it('newItem starts with empty child collections', () => {
		expect(enemyEntity.newItem(5)).toEqual({
			id: 5,
			name: '',
			isBoss: false,
			designerNotes: '',
			attributeDistribution: [],
			skillPool: [],
			spawns: [],
			bossZones: []
		});
	});

	it('badges only bosses', () => {
		expect(enemyEntity.listBadge?.({ ...baseline, isBoss: true })).toBe('Boss');
		expect(enemyEntity.listBadge?.(baseline)).toBeNull();
	});

	it('surfaces the attribute, skill and zone counts in the list meta', () => {
		expect(enemyEntity.meta(baseline)).toEqual([
			['attr', 1],
			['skill', 1],
			['zone', 1]
		]);
	});

	it('colours the list badge with the enemy accent', () => {
		expect(enemyEntity.badgeColor?.(baseline)).toBe('var(--enemy-accent)');
	});

	it('persist saves the attribute distribution and spawns when they change', async () => {
		const record: WorkbenchEnemy = {
			...baseline,
			attributeDistribution: [{ attributeId: 0, baseAmount: 2, amountPerLevel: 1 }], // changed
			spawns: [{ zoneId: 0, weight: 9 }] // changed
		};
		socket.enemies = [record];

		await enemyEntity.persist({ added: [], modified: [{ record, baseline }], deleted: [], existingIds: [0] });

		expect(postBodyTo('AdminTools/SetEnemyAttributeDistributions')).toEqual({
			enemyId: 0,
			attributeDistributions: [{ attributeId: 0, baseAmount: 2, amountPerLevel: 1 }]
		});
		expect(postBodyTo('AdminTools/SetEnemySpawns')).toEqual({ enemyId: 0, spawns: [{ zoneId: 0, weight: 9 }] });
		// The skill pool was untouched, so its endpoint is skipped.
		expect(postBodyTo('AdminTools/SetEnemySkills')).toBeUndefined();
	});

	it('persist strips the child collections off the identity DTO and saves only the changed child', async () => {
		const record: WorkbenchEnemy = { ...baseline, name: 'Cave Bat', skillPool: [1, 2] }; // identity + skills changed
		socket.enemies = [record];

		await enemyEntity.persist({ added: [], modified: [{ record, baseline }], deleted: [], existingIds: [0] });

		// The identity Edit carries no child data — those go through their own endpoints.
		expect(postBodyTo('AdminTools/AddEditEnemies')[0].item).toEqual({
			id: 0,
			name: 'Cave Bat',
			isBoss: false,
			designerNotes: '',
			attributeDistribution: [],
			skillPool: [],
			spawns: []
		});
		// Only the skill pool actually changed, so only its endpoint is hit.
		expect(postBodyTo('AdminTools/SetEnemySkills')).toEqual({ enemyId: 0, skillIds: [1, 2] });
		expect(postBodyTo('AdminTools/SetEnemyAttributeDistributions')).toBeUndefined();
		expect(postBodyTo('AdminTools/SetEnemySpawns')).toBeUndefined();
	});

	it('persist Adds a new enemy and saves its children against the resolved id', async () => {
		// A freshly-added record carries a temporary negative id; after the identity Add the
		// backend appends it at a real id, which persistEntity resolves before the child savers run.
		const added: WorkbenchEnemy = {
			id: -1,
			name: 'New Enemy',
			designerNotes: '',
			isBoss: true,
			attributeDistribution: [{ attributeId: 0, baseAmount: 3, amountPerLevel: 1 }],
			skillPool: [2],
			spawns: [{ zoneId: 1, weight: 8 }],
			bossZones: []
		};
		socket.enemies = [{ ...added, id: 7 }]; // the persisted record at its real id

		await enemyEntity.persist({ added: [added], modified: [], deleted: [], existingIds: [] });

		// Identity Add posted with the child collections stripped.
		const addCall = postBodyTo('AdminTools/AddEditEnemies');
		expect(addCall[0].changeType).toBe(EChangeType.Add);
		expect(addCall[0].item).toEqual({
			id: -1,
			name: 'New Enemy',
			isBoss: true,
			designerNotes: '',
			attributeDistribution: [],
			skillPool: [],
			spawns: []
		});
		// Every child saver runs against the RESOLVED id (7), not the temporary -1.
		expect(postBodyTo('AdminTools/SetEnemyAttributeDistributions')).toEqual({
			enemyId: 7,
			attributeDistributions: [{ attributeId: 0, baseAmount: 3, amountPerLevel: 1 }]
		});
		expect(postBodyTo('AdminTools/SetEnemySkills')).toEqual({ enemyId: 7, skillIds: [2] });
		expect(postBodyTo('AdminTools/SetEnemySpawns')).toEqual({ enemyId: 7, spawns: [{ zoneId: 1, weight: 8 }] });
	});

	it('persist Adds a bare new enemy without posting empty child collections (#1895)', async () => {
		const added: WorkbenchEnemy = {
			id: -1,
			name: 'New Enemy',
			designerNotes: '',
			isBoss: false,
			attributeDistribution: [],
			skillPool: [],
			spawns: [],
			bossZones: []
		};
		socket.enemies = [{ ...added, id: 7 }];

		await enemyEntity.persist({ added: [added], modified: [], deleted: [], existingIds: [] });

		expect(postBodyTo('AdminTools/AddEditEnemies')[0].changeType).toBe(EChangeType.Add);
		// Nothing was ever added to any child collection, so none of their setters should post.
		expect(postBodyTo('AdminTools/SetEnemyAttributeDistributions')).toBeUndefined();
		expect(postBodyTo('AdminTools/SetEnemySkills')).toBeUndefined();
		expect(postBodyTo('AdminTools/SetEnemySpawns')).toBeUndefined();
	});

	it('refresh derives bossZones from the zones whose dedicated boss is this enemy', async () => {
		socket.enemies = [
			{ id: 0, name: 'Bat', isBoss: false, attributeDistribution: [], skillPool: [], spawns: [] },
			{ id: 7, name: 'Warden', isBoss: true, attributeDistribution: [], skillPool: [], spawns: [] }
		];
		socket.zones = [
			{ id: 0, name: 'Hollow', description: '', order: 0, levelMin: 1, levelMax: 5, bossLevel: 1 },
			{ id: 1, name: 'Cavern', description: '', order: 1, levelMin: 5, levelMax: 10, bossEnemyId: 7, bossLevel: 3 },
			{ id: 2, name: 'Grotto', description: '', order: 2, levelMin: 8, levelMax: 12, bossEnemyId: 7, bossLevel: 4 }
		];

		const rows = await enemyEntity.refresh();

		// The standard enemy is no zone's dedicated boss; the boss is assigned to two zones.
		expect(rows[0].bossZones).toEqual([]);
		expect(rows[1].bossZones).toEqual([1, 2]);
		// The raw catalogues are cached for the rest of the workbench.
		expect(staticData.enemies).toBe(socket.enemies);
		expect(staticData.zones).toBe(socket.zones);
	});

	it('headline names the zones this enemy is the dedicated boss of, or is blank otherwise', () => {
		// Zones are indexed by id (Id-as-index), so the names resolve from their slots.
		const zones: unknown[] = [];
		zones[1] = { id: 1, name: 'Cavern' };
		zones[2] = { id: 2, name: 'Grotto' };
		staticData.zones = zones;
		expect(enemyEntity.headline?.({ ...baseline, isBoss: true, bossZones: [1, 2] })).toBe(
			'Dedicated boss of Cavern, Grotto'
		);
		expect(enemyEntity.headline?.(baseline)).toBe('');
	});

	it('spawns warn clears for a boss assigned to a zone but still flags an unassigned enemy', () => {
		const warn = spawnsWarn();
		// A dedicated boss with no random spawns is valid — it appears via the zone's boss FK.
		expect(warn?.({ ...baseline, isBoss: true, spawns: [], bossZones: [1] })).toBeNull();
		// A random spawn alone is still valid.
		expect(warn?.({ ...baseline, spawns: [{ zoneId: 0, weight: 5 }], bossZones: [] })).toBeNull();
		// Neither a spawn nor a boss assignment → it never appears, so warn.
		expect(warn?.({ ...baseline, spawns: [], bossZones: [] })).toBe('Not assigned to any zone');
	});

	describe('newRow factories', () => {
		it('attribute newRow picks the first free attribute and zeroes the amounts', () => {
			// Strength (id 0) is already taken, so the first free attribute (id 1) is chosen.
			const enemy: WorkbenchEnemy = {
				...baseline,
				attributeDistribution: [{ attributeId: 0, baseAmount: 1, amountPerLevel: 0 }]
			};
			expect(tableSection('attrs').newRow(enemy)).toEqual({ attributeId: 1, baseAmount: 0, amountPerLevel: 0 });
		});

		it('spawn newRow picks the first unused zone with the default weight', () => {
			staticData.zones = [
				{ id: 0, name: 'Verdant Hollow', levelMin: 1, levelMax: 5 },
				{ id: 1, name: 'Frost Cavern', levelMin: 6, levelMax: 10 }
			];
			// Zone 0 is already assigned, so zone 1 is the first free option.
			const enemy: WorkbenchEnemy = { ...baseline, spawns: [{ zoneId: 0, weight: 5 }] };
			expect(tableSection('spawns').newRow(enemy)).toEqual({ zoneId: 1, weight: 5 });
		});
	});
});
