import { describe, it, expect, beforeEach, vi } from 'vitest';
import { flushSync } from 'svelte';
import {
	EAttribute,
	EChallengeGoalComparison,
	EChallengeType,
	EDamageTypeKey,
	EEntityType,
	EItemCategory,
	EItemModType,
	ERarity,
	type IChallenge,
	type IChallengeType,
	type IItem,
	type IItemMod,
	type IPlayerChallenge
} from '$lib/api';

// The view-model reads the challenge catalogue from the reference-data store and the player's
// progress from the playerChallenges store. Mock staticData with a small fixture, but keep the REAL
// playerChallenges store (only its socket fetch stubbed) so the view-model is exercised against the
// genuine reactive `$state` — a live `markCompleted()` push then re-runs its `$derived` chain.
const { staticData, mockFetchSocket } = vi.hoisted(() => ({
	staticData: {
		challenges: [] as IChallenge[],
		challengeTypes: [] as IChallengeType[],
		items: [] as IItem[],
		itemMods: [] as IItemMod[],
		enemies: [] as { id: number; name: string }[],
		zones: [] as { id: number; name: string; order?: number; unlockChallengeId?: number }[],
		skills: [] as { id: number; name: string; rarityId?: ERarity }[]
	},
	mockFetchSocket: vi.fn()
}));

vi.mock('$lib/api', async (importOriginal) => {
	const actual = await importOriginal<typeof import('$lib/api')>();
	return { ...actual, fetchSocketData: mockFetchSocket };
});
vi.mock('$stores', async (importOriginal) => {
	const actual = await importOriginal<typeof import('$stores')>();
	return { ...actual, staticData };
});

import { playerChallenges } from '$stores';

import {
	ChallengesView,
	buildChallengeVM,
	groupByType,
	nextUp,
	overallSummary,
	progressInfo,
	resolveReward,
	sortChallenges,
	typeStats
} from '$routes/game/screens/challenges/challenges-view.svelte';

const item = (id: number, name: string, rarity: ERarity, cat: EItemCategory, extra: Partial<IItem> = {}): IItem => ({
	id,
	name,
	description: `${name} description`,
	designerNotes: '',
	itemCategoryId: cat,
	rarityId: rarity,
	iconPath: '',
	requiredProficiencyLevel: 0,
	attributes: [{ attributeId: EAttribute.Strength, amount: 5 }],
	modSlots: [{ id: id * 10, itemId: id, itemModSlotTypeId: EItemModType.Prefix }],
	tags: [],
	...extra
});

const mod = (id: number, name: string, rarity: ERarity): IItemMod => ({
	id,
	name,
	description: `${name} description`,
	designerNotes: '',
	itemModTypeId: EItemModType.Prefix,
	rarityId: rarity,
	attributes: [{ attributeId: EAttribute.Agility, amount: 3 }],
	tags: []
});

const challenge = (over: Partial<IChallenge> & Pick<IChallenge, 'id' | 'name' | 'challengeTypeId'>): IChallenge => ({
	description: `${over.name} description`,
	designerNotes: '',
	entityType: EEntityType.None,
	progressGoal: 10,
	...over
});

beforeEach(() => {
	staticData.items = [];
	staticData.itemMods = [];
	staticData.items[3] = item(3, 'Aegis Greathelm', ERarity.Rare, EItemCategory.Helm);
	staticData.items[25] = item(25, 'Sunder Ring', ERarity.Mythic, EItemCategory.Accessory);
	staticData.itemMods[213] = mod(213, 'Honed', ERarity.Rare);

	staticData.enemies = [];
	staticData.enemies[1] = { id: 1, name: 'Goblin' };
	staticData.zones = [];
	staticData.zones[2] = { id: 2, name: 'Forgotten Catacombs' };

	staticData.challengeTypes = [
		{ id: EChallengeType.EnemiesKilled, goalComparison: EChallengeGoalComparison.AtLeast, name: 'Enemies Killed' },
		{ id: EChallengeType.TimeTrial, goalComparison: EChallengeGoalComparison.AtMost, name: 'Time Trial' }
	];

	staticData.challenges = [
		challenge({
			id: 1,
			name: 'First Blood',
			challengeTypeId: EChallengeType.EnemiesKilled,
			progressGoal: 10,
			rewardItemId: 3
		}),
		challenge({
			id: 2,
			name: 'Goblin Bane',
			challengeTypeId: EChallengeType.EnemiesKilled,
			entityType: EEntityType.Enemy,
			targetEntityId: 1,
			progressGoal: 50,
			rewardItemModId: 213
		}),
		challenge({
			id: 9,
			name: 'Quicksilver',
			challengeTypeId: EChallengeType.TimeTrial,
			progressGoal: 60,
			rewardItemId: 25
		})
	];
});

describe('resolveReward', () => {
	it('resolves an item reward with rarity, teaser sub-line and a tooltip preview item', () => {
		const reward = resolveReward(staticData.challenges[0], true);
		expect(reward).toMatchObject({ kind: 'item', revealed: true, rarity: ERarity.Rare, name: 'Aegis Greathelm' });
		expect(reward?.sub).toBe('Rare · Helm');
		// The preview item carries base stats for the (re-used) item tooltip.
		expect(reward?.item?.totalAttributes.getAttributeMap()).toEqual([{ name: 'Strength', value: 5 }]);
	});

	it('resolves a mod reward and honours the revealed flag', () => {
		const reward = resolveReward(staticData.challenges[1], false);
		expect(reward).toMatchObject({ kind: 'mod', revealed: false, rarity: ERarity.Rare, name: 'Honed' });
		expect(reward?.sub).toBe('Rare · Prefix');
		expect(reward?.mod?.id).toBe(213);
	});

	it('returns null when the challenge has no reward', () => {
		expect(
			resolveReward(challenge({ id: 99, name: 'No Reward', challengeTypeId: EChallengeType.EnemiesKilled }), true)
		).toBeNull();
	});
});

describe('progressInfo', () => {
	it('computes an accumulating (atLeast) fill clamped to 100%', () => {
		const ch = challenge({ id: 1, name: 'x', challengeTypeId: EChallengeType.EnemiesKilled, progressGoal: 50 });
		expect(progressInfo(ch, EChallengeGoalComparison.AtLeast, 25)).toMatchObject({
			atMost: false,
			value: 25,
			goal: 50,
			percent: 50
		});
		expect(progressInfo(ch, EChallengeGoalComparison.AtLeast, 80).percent).toBe(100);
	});

	it('treats an atMost goal as best-vs-target proximity, with 0 meaning "no data"', () => {
		const ch = challenge({ id: 9, name: 'x', challengeTypeId: EChallengeType.TimeTrial, progressGoal: 60 });
		// no qualifying value yet
		expect(progressInfo(ch, EChallengeGoalComparison.AtMost, 0)).toMatchObject({
			atMost: true,
			hasData: false,
			percent: 0
		});
		// best 120s vs 60s target -> 50% of the way there
		expect(progressInfo(ch, EChallengeGoalComparison.AtMost, 120)).toMatchObject({
			best: 120,
			target: 60,
			percent: 50,
			hasData: true
		});
		// already beating the target clamps to 100%
		expect(progressInfo(ch, EChallengeGoalComparison.AtMost, 52).percent).toBe(100);
	});
});

describe('buildChallengeVM', () => {
	it('derives done/active/locked state and only reveals the reward when complete', () => {
		const done = buildChallengeVM(staticData.challenges[0], {
			challengeId: 1,
			progress: 10,
			completed: true,
			completedAt: 'today'
		});
		expect(done.state).toBe('done');
		expect(done.reward?.revealed).toBe(true);
		expect(done.completedAt).toBe('today');

		const active = buildChallengeVM(staticData.challenges[0], { challengeId: 1, progress: 4, completed: false });
		expect(active.state).toBe('active');
		expect(active.reward?.revealed).toBe(false);

		const locked = buildChallengeVM(staticData.challenges[0], undefined);
		expect(locked.state).toBe('locked');
		expect(locked.progress).toBe(0);
	});

	it('resolves the target entity name from the matching reference pool', () => {
		expect(buildChallengeVM(staticData.challenges[1]).target).toBe('Goblin');
		expect(buildChallengeVM(staticData.challenges[0]).target).toBeNull();
	});

	it('resolves a DamageType target via the fixed damage-type-key labels, not a reference pool', () => {
		const ch = challenge({
			id: 3,
			name: 'Firebrand',
			challengeTypeId: EChallengeType.KillsByDamageType,
			entityType: EEntityType.DamageType,
			targetEntityId: EDamageTypeKey.Fire,
			progressGoal: 20
		});
		expect(buildChallengeVM(ch).target).toBe('Fire');
	});

	it("uses the type's intrinsic comparison so a TimeTrial reads as a minimisation goal", () => {
		const vm = buildChallengeVM(staticData.challenges[2], { challengeId: 9, progress: 52, completed: false });
		expect(vm.prog.atMost).toBe(true);
		expect(vm.prog.target).toBe(60);
	});

	it('lists the zones the challenge unlocks (gated on it), in authored order', () => {
		staticData.zones = [];
		staticData.zones[40] = { id: 40, name: 'Sunken Vault', order: 4, unlockChallengeId: 1 };
		staticData.zones[30] = { id: 30, name: 'Ashen Pass', order: 3, unlockChallengeId: 1 };
		staticData.zones[20] = { id: 20, name: 'Other', order: 2, unlockChallengeId: 2 };

		expect(buildChallengeVM(staticData.challenges[0]).unlocksZones).toEqual(['Ashen Pass', 'Sunken Vault']);
		// A challenge that gates no zone reports none.
		expect(buildChallengeVM(staticData.challenges[2]).unlocksZones).toEqual([]);
	});
});

describe('grouping, stats and sorting', () => {
	const vms = () => staticData.challenges.map((c) => buildChallengeVM(c));

	it('groups by type in enum order, dropping empty types', () => {
		const groups = groupByType(vms());
		expect(groups.map((g) => g.typeId)).toEqual([EChallengeType.EnemiesKilled, EChallengeType.TimeTrial]);
		expect(groups[0].label).toBe('Enemies Killed');
		expect(typeStats(groups[0].items)).toEqual({ total: 2, done: 0 });
	});

	it('sorts by progress (active-by-closeness → locked → done), rarity and name', () => {
		const list = [
			buildChallengeVM(staticData.challenges[0], { challengeId: 1, progress: 9, completed: false }), // active 90%
			buildChallengeVM(staticData.challenges[1], { challengeId: 2, progress: 0, completed: false }), // locked
			buildChallengeVM(staticData.challenges[2], { challengeId: 9, progress: 52, completed: true, completedAt: 'now' }) // done
		];
		expect(sortChallenges(list, 'progress').map((c) => c.id)).toEqual([1, 2, 9]);
		// rarity: Mythic (id 9) before Rare (ids 1, 2)
		expect(sortChallenges(list, 'rarity').map((c) => c.id)[0]).toBe(9);
		expect(sortChallenges(list, 'name').map((c) => c.name)).toEqual(['First Blood', 'Goblin Bane', 'Quicksilver']);
	});
});

describe('summary helpers', () => {
	it('summarises totals and finds the in-progress challenge closest to completion', () => {
		const list = [
			buildChallengeVM(staticData.challenges[0], { challengeId: 1, progress: 3, completed: false }), // active 30%
			buildChallengeVM(staticData.challenges[1], { challengeId: 2, progress: 40, completed: false }), // active 80%
			buildChallengeVM(staticData.challenges[2], { challengeId: 9, progress: 52, completed: true, completedAt: 'now' })
		];
		expect(overallSummary(list)).toEqual({ total: 3, done: 1, active: 2, pct: 33 });
		expect(nextUp(list)?.id).toBe(2);
	});
});

describe('ChallengesView', () => {
	const player: IPlayerChallenge[] = [
		{ challengeId: 1, progress: 10, completed: true, completedAt: 'today' },
		{ challengeId: 2, progress: 32, completed: false },
		{ challengeId: 9, progress: 0, completed: false }
	];

	beforeEach(async () => {
		// Seed the real store through its socket fetch so the view-model reads genuine reactive state.
		playerChallenges.reset();
		mockFetchSocket.mockResolvedValue(player.map((p) => ({ ...p })));
		await playerChallenges.load(true);
	});

	it('builds the reactive view-model from store + player progress', () => {
		const view = new ChallengesView();
		expect(view.all).toHaveLength(3);
		expect(view.summary).toMatchObject({ total: 3, done: 1 });
		expect(view.groups.map((g) => g.typeId)).toEqual([EChallengeType.EnemiesKilled, EChallengeType.TimeTrial]);
	});

	it('hides a retired-incomplete challenge but keeps a retired-completed one (out of circulation)', () => {
		// Retire challenge 9 (incomplete) and challenge 1 (already completed). A retired challenge the
		// player never completed leaves circulation and is hidden; a completed one stays visible as done.
		staticData.challenges = staticData.challenges.map((ch) =>
			ch.id === 9 || ch.id === 1 ? { ...ch, retiredAt: '2026-01-01T00:00:00Z' } : ch
		);

		const view = new ChallengesView();

		const ids = view.all.map((c) => c.id);
		expect(ids).toContain(1); // retired but completed → still shown
		expect(ids).toContain(2); // active, not retired → shown
		expect(ids).not.toContain(9); // retired and incomplete → hidden
	});

	it('switches the detail pane and resorts when selection/sort change', () => {
		const view = new ChallengesView();

		expect(view.selectedGroup).toBeNull(); // overview by default
		view.select(EChallengeType.EnemiesKilled);
		expect(view.selectedGroup?.typeId).toBe(EChallengeType.EnemiesKilled);
		expect(view.detail.map((c) => c.id)).toEqual([2, 1]); // active (id 2) before done (id 1)

		view.setSort('name');
		expect(view.detail.map((c) => c.name)).toEqual(['First Blood', 'Goblin Bane']);
	});

	it('reflects a live markCompleted() push without re-seeding the view (issue #908)', () => {
		const view = new ChallengesView();
		view.select(EChallengeType.EnemiesKilled);

		let detailIds: number[] = [];
		let summaryDone = -1;
		const cleanup = $effect.root(() => {
			$effect(() => {
				detailIds = view.detail.map((c) => c.id);
				summaryDone = view.summary.done;
			});
		});

		flushSync();
		// Challenge 2 starts in progress (active) and sorts ahead of the completed challenge 1.
		expect(detailIds).toEqual([2, 1]);
		expect(summaryDone).toBe(1);

		// A live ChallengeCompleted push on the shared store must flip the card without a remount.
		playerChallenges.markCompleted(2);
		flushSync();
		expect(summaryDone).toBe(2);
		// Now both are done; the progress sort moves the freshly-completed challenge to the done bucket.
		expect(detailIds).toEqual([1, 2]);

		cleanup();
	});
});
