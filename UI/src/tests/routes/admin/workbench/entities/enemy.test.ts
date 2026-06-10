import { describe, it, expect, beforeEach, vi } from 'vitest';
import type { IEnemy } from '$lib/api';

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
});
