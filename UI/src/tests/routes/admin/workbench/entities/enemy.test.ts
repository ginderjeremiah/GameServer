import { describe, it, expect, beforeEach, vi } from 'vitest';
import { EChangeType, type IEnemy } from '$lib/api';
import type { TableSectionConfig } from '$routes/admin/workbench/entities/types';

/* Enemy config transforms: `newItem` defaults, the boss list badge, and the
   persist path — the identity DTO strips the child collections, and only the
   child collection that actually changed is saved. `fetchSocketData`/`ApiRequest`
   are stubbed; the real `persistEntity` orchestration runs unmocked. */

const { staticData, socket, mockPost, mockFetch } = vi.hoisted(() => {
	const socket = { enemies: [] as unknown[] };
	return {
		// eslint-disable-next-line @typescript-eslint/no-explicit-any
		staticData: {} as any,
		socket,
		mockPost: vi.fn(),
		mockFetch: vi.fn(async (command: string) => (command === 'GetEnemies' ? socket.enemies : []))
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

import { enemyEntity } from '$routes/admin/workbench/entities/enemy';

/** Finds the body posted to a given AdminTools endpoint (or undefined if never called). */
const postBodyTo = (endpoint: string) => mockPost.mock.calls.find((c) => c[0] === endpoint)?.[1];

/** A table section's config by key (for exercising its `newRow` factory). */
const tableSection = (key: string) => enemyEntity.sections.find((s) => s.key === key) as TableSectionConfig<IEnemy>;

beforeEach(() => {
	mockPost.mockReset().mockResolvedValue(undefined);
	mockFetch.mockClear();
	socket.enemies = [];
	for (const key of Object.keys(staticData)) {
		delete staticData[key];
	}
});

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
		const record: IEnemy = {
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

	it('persist Adds a new enemy and saves its children against the resolved id', async () => {
		// A freshly-added record carries a temporary negative id; after the identity Add the
		// backend appends it at a real id, which persistEntity resolves before the child savers run.
		const added: IEnemy = {
			id: -1,
			name: 'New Enemy',
			isBoss: true,
			attributeDistribution: [{ attributeId: 0, baseAmount: 3, amountPerLevel: 1 }],
			skillPool: [2],
			spawns: [{ zoneId: 1, weight: 8 }]
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

	describe('newRow factories', () => {
		it('attribute newRow picks the first free attribute and zeroes the amounts', () => {
			// Strength (id 0) is already taken, so the first free attribute (id 1) is chosen.
			const enemy: IEnemy = {
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
			const enemy: IEnemy = { ...baseline, spawns: [{ zoneId: 0, weight: 5 }] };
			expect(tableSection('spawns').newRow(enemy)).toEqual({ zoneId: 1, weight: 5 });
		});
	});
});
