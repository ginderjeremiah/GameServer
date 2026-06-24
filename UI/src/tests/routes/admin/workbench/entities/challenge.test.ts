import { describe, it, expect, beforeEach, vi } from 'vitest';
import {
	EChallengeGoalComparison,
	EChallengeType,
	EEntityType,
	EStatisticType,
	type IChallenge,
	type IChallengeType
} from '$lib/api';

/* Challenge config transforms: `newItem` deriving the tracked statistic/entity
   from the default type, the derived display data (listBadge/headline/meta), and
   the whole-challenge identity DTO on persist. The challenge-domain helpers it
   leans on (deriveFromType, challengeSentence) have their own suite. The
   `fetchSocketData`/`ApiRequest` boundary is stubbed; `persistEntity` runs real. */

const { staticData, socket, mockPost, mockFetch } = vi.hoisted(() => {
	const socket = { challenges: [] as unknown[] };
	return {
		// eslint-disable-next-line @typescript-eslint/no-explicit-any
		staticData: {} as any,
		socket,
		mockPost: vi.fn(),
		mockFetch: vi.fn(async (command: string) => (command === 'GetChallenges' ? socket.challenges : []))
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

import { challengeEntity } from '$routes/admin/workbench/entities/challenge';
import { reference } from '$routes/admin/workbench/reference.svelte';

/** Finds the body posted to a given AdminTools endpoint (or undefined if never called). */
const postBodyTo = (endpoint: string) => mockPost.mock.calls.find((c) => c[0] === endpoint)?.[1];

// Mirrors the GetChallengeTypes metadata the config reads for its derivations.
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
	mockPost.mockReset().mockResolvedValue(undefined);
	mockFetch.mockClear();
	socket.challenges = [];
	for (const key of Object.keys(staticData)) {
		delete staticData[key];
	}
	reference.challengeTypes = TYPES;
});

describe('challengeEntity', () => {
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
