import { describe, it, expect, beforeEach, vi } from 'vitest';
import { flushSync } from 'svelte';
import {
	EAttribute,
	EChallengeType,
	EEntityType,
	EModifierType,
	ERarity,
	ESkillAcquisition,
	ESkillEffectTarget,
	EStatisticType,
	type IPlayerStatistic
} from '$lib/api';
import { SERVER_STAT_TYPES } from '../stats/stat-fixtures';

// CodexView reads reference data + challenge progress + per-zone clears from the stores, and reuses
// the Statistics screen's query engine — all mocked here. (The Statistics view-model also imports
// `navigation`.)
const { staticData, playerChallenges, navigation, statistics } = vi.hoisted(() => {
	let mockStats: IPlayerStatistic[] = [];
	return {
		// eslint-disable-next-line @typescript-eslint/no-explicit-any
		staticData: {} as any,
		playerChallenges: {
			// eslint-disable-next-line @typescript-eslint/no-explicit-any
			all: [] as any[],
			isChallengeCompleted(id: number) {
				// eslint-disable-next-line @typescript-eslint/no-explicit-any
				return this.all.some((c: any) => c.challengeId === id && c.completed);
			}
		},
		navigation: { requestScreen: vi.fn(), consumePayload: vi.fn(), clear: vi.fn() },
		statistics: {
			isZoneCleared: vi.fn<(id: number) => boolean>(() => false),
			get stats() {
				return mockStats;
			},
			set stats(value: IPlayerStatistic[]) {
				mockStats = value;
			}
		}
	};
});
vi.mock('$stores', () => ({ staticData, playerChallenges, navigation, statistics }));

import { CodexView, type EnemyRowVM, type ZoneProjectionVM } from '$routes/game/screens/codex/codex-view.svelte';

const dist = () => [
	{ attributeId: EAttribute.Strength, baseAmount: 10, amountPerLevel: 1 },
	{ attributeId: EAttribute.Endurance, baseAmount: 8, amountPerLevel: 0.8 },
	{ attributeId: EAttribute.Intellect, baseAmount: 3, amountPerLevel: 0.2 },
	{ attributeId: EAttribute.Agility, baseAmount: 6, amountPerLevel: 0.5 },
	{ attributeId: EAttribute.Dexterity, baseAmount: 5, amountPerLevel: 0.4 },
	{ attributeId: EAttribute.Luck, baseAmount: 3, amountPerLevel: 0.2 }
];

function seed(): void {
	staticData.zones = [
		{
			id: 0,
			name: 'Emberreach',
			description: 'Ash-strewn lowlands.',
			order: 1,
			levelMin: 1,
			levelMax: 10,
			bossEnemyId: 2,
			bossLevel: 10
		},
		// Ashfen Marsh is gated on challenge 0 (uncompleted in the seed) — a locked zone.
		{
			id: 1,
			name: 'Ashfen Marsh',
			description: 'A drowned bog.',
			order: 2,
			levelMin: 11,
			levelMax: 22,
			bossLevel: 22,
			unlockChallengeId: 0
		},
		{
			id: 2,
			name: 'Sunken Causeway',
			description: 'A broken road.',
			order: 3,
			levelMin: 18,
			levelMax: 28,
			bossLevel: 28
		}
	];
	staticData.enemies = [
		{
			id: 0,
			name: 'Dust Skitterer',
			isBoss: false,
			attributeDistribution: dist(),
			skillPool: [0, 1],
			spawns: [
				{ zoneId: 0, weight: 60 },
				{ zoneId: 1, weight: 20 }
			]
		},
		{
			id: 1,
			name: 'Bog Lurker',
			isBoss: false,
			attributeDistribution: dist(),
			skillPool: [1],
			spawns: [{ zoneId: 1, weight: 40 }]
		},
		{ id: 2, name: 'Cinder Tyrant', isBoss: true, attributeDistribution: dist(), skillPool: [0, 1], spawns: [] }
	];
	staticData.skills = [
		// Cleave: a Strength-scaling attack used by two enemies (one a boss).
		{
			id: 0,
			name: 'Cleave',
			description: 'A wide sweeping strike.',
			baseDamage: 14,
			cooldownMs: 1800,
			rarityId: ERarity.Rare,
			acquisition: ESkillAcquisition.Player,
			damageMultipliers: [{ attributeId: EAttribute.Strength, multiplier: 1.5 }],
			effects: []
		},
		// War Cry: a utility buff/debuff with no base damage, used by every enemy; granted by an item.
		{
			id: 1,
			name: 'War Cry',
			description: 'A rallying shout.',
			baseDamage: 0,
			cooldownMs: 6000,
			acquisition: ESkillAcquisition.Item,
			damageMultipliers: [],
			effects: [
				{
					id: 0,
					target: ESkillEffectTarget.Self,
					attributeId: EAttribute.Strength,
					modifierTypeId: EModifierType.Additive,
					amount: 15,
					durationMs: 5000,
					scalingAttributeId: EAttribute.Strength,
					scalingAmount: 0
				},
				{
					id: 1,
					target: ESkillEffectTarget.Opponent,
					attributeId: EAttribute.Toughness,
					modifierTypeId: EModifierType.Multiplicative,
					amount: 0.5,
					durationMs: 4000,
					scalingAttributeId: EAttribute.Strength,
					scalingAmount: 0
				}
			]
		},
		// Focus: no enemy lists it in its pool, and it's Enemy-flagged with no player source → enemy-only.
		{
			id: 2,
			name: 'Focus',
			description: 'Center your mind.',
			baseDamage: 0,
			cooldownMs: 0,
			acquisition: ESkillAcquisition.Enemy,
			damageMultipliers: [],
			effects: []
		}
	];
	// One item, a Rare staff, grants War Cry (skill 1) — the Item acquisition channel.
	staticData.items = [{ id: 0, name: 'Ember Staff', rarityId: ERarity.Rare, grantedSkillId: 1, retiredAt: undefined }];
	staticData.challenges = [
		{
			id: 0,
			name: 'Cull the Skitterers',
			challengeTypeId: EChallengeType.EnemiesKilled,
			entityType: EEntityType.Enemy,
			targetEntityId: 0,
			progressGoal: 100
		},
		{
			id: 1,
			name: 'The Tyrant Falls',
			challengeTypeId: EChallengeType.BossesDefeated,
			entityType: EEntityType.Enemy,
			targetEntityId: 2,
			progressGoal: 1
		}
	];
	staticData.challengeTypes = [
		{ id: EChallengeType.EnemiesKilled, goalComparison: 1, name: 'Enemies Killed' },
		{ id: EChallengeType.BossesDefeated, goalComparison: 1, name: 'Bosses Defeated' }
	];
	staticData.statisticTypes = SERVER_STAT_TYPES;
	staticData.attributes = [
		{ id: EAttribute.Strength, name: 'Strength', code: 'STR', isHarmful: false },
		{ id: EAttribute.Toughness, name: 'Toughness', code: '', isHarmful: false }
	];
	playerChallenges.all = [{ challengeId: 0, progress: 62, completed: false }];
}

const STATS: IPlayerStatistic[] = [
	{ statisticTypeId: EStatisticType.EnemiesKilled, entityId: 0, value: 100 },
	{ statisticTypeId: EStatisticType.EnemiesKilled, entityId: 1, value: 20 },
	{ statisticTypeId: EStatisticType.ZonesCleared, entityId: 1, value: 3 },
	{ statisticTypeId: EStatisticType.DamageDealt, entityId: 0, value: 4500 }
];

beforeEach(() => {
	seed();
	navigation.requestScreen.mockClear();
	// Zone 0 (Emberreach) is cleared by default; individual tests override as needed.
	statistics.isZoneCleared.mockReset();
	statistics.isZoneCleared.mockImplementation((id: number) => id === 0);
	statistics.stats = [];
});

describe('CodexView tabs', () => {
	it('reports live counts per section', () => {
		const tabs = new CodexView().tabs;
		expect(tabs.map((t) => [t.key, t.count])).toEqual([
			['enemies', 3],
			['zones', 3],
			['skills', 3]
		]);
	});
});

describe('CodexView enemy rows', () => {
	it('projects zone + skill counts (boss shows a single zone)', () => {
		const rows = new CodexView().enemiesTab.enemyRows;
		expect(rows.find((r) => r.id === 0)).toMatchObject({ zoneCount: 2, skillCount: 2, band: '1–22' });
		expect(rows.find((r) => r.id === 2)).toMatchObject({ zoneCount: 1, skillCount: 2, band: 'L10', isBoss: true });
	});

	it('filters by normal / boss', () => {
		const view = new CodexView();
		view.enemiesTab.setFilter('boss');
		expect(view.enemiesTab.enemyRows.map((r) => r.id)).toEqual([2]);
		view.enemiesTab.setFilter('normal');
		expect(view.enemiesTab.enemyRows.map((r) => r.id)).toEqual([0, 1]);
	});

	it('exposes the level sort key + search haystack on each row', () => {
		const rows = new CodexView().enemiesTab.enemyRows;
		// Dust Skitterer spans 1–22 (low end 1); the haystack carries name, kind and spawn zones.
		const skitterer = rows.find((r) => r.id === 0);
		expect(skitterer?.level).toBe(1);
		expect(skitterer?.searchText).toContain('dust skitterer');
		expect(skitterer?.searchText).toContain('enemy');
		expect(skitterer?.searchText).toContain('emberreach');
		// Cinder Tyrant is a boss fixed at level 10.
		expect(rows.find((r) => r.id === 2)).toMatchObject({ level: 10 });
	});
});

describe('CodexView enemy projections', () => {
	it('projects every enemy independent of the active filter, search and sort', () => {
		const view = new CodexView();
		// The immutable projection ignores the interaction state the thin `enemyRows` layers on top.
		view.enemiesTab.setFilter('boss');
		view.enemiesTab.search = 'griffin';
		view.enemiesTab.sort = 'name';
		expect(view.enemiesTab.enemyProjections.map((p) => p.id)).toEqual([0, 1, 2]);
		expect(view.enemiesTab.enemyProjections.find((p) => p.id === 2)).toMatchObject({
			band: 'L10',
			level: 10,
			isBoss: true,
			zoneCount: 1,
			skillCount: 2
		});
		expect(view.enemiesTab.enemyProjections.find((p) => p.id === 0)?.searchText).toContain('emberreach');
	});

	it('memoizes the projection on a single instance: the same reference survives search / sort / filter changes', () => {
		// The cross-instance value test above proves the projection's *value* ignores interaction state.
		// This asserts the actual perf guarantee the projection/row split exists for: on one live instance,
		// the heavy projection is not rebuilt when the cheap interaction state the rows layer reads moves —
		// it returns the *same reference*. Run inside an effect root so the deriveds stay live and memoized.
		const view = new CodexView();
		let projection: EnemyRowVM[] | undefined;
		let rows: EnemyRowVM[] | undefined;
		const cleanup = $effect.root(() => {
			$effect(() => {
				projection = view.enemiesTab.enemyProjections;
			});
			$effect(() => {
				rows = view.enemiesTab.enemyRows;
			});
		});

		flushSync();
		const projectionBefore = projection;
		const rowsBefore = rows;

		// Mutate the reactive interaction state (`filter`/`search`/`sort` are `$state`) the row layer reads.
		view.enemiesTab.setFilter('boss');
		view.enemiesTab.search = 'griffin';
		view.enemiesTab.sort = 'name';
		flushSync();

		// The interaction change genuinely moved the reactive graph — the thin row layer recomputed…
		expect(rows).not.toBe(rowsBefore);
		expect(rows).toEqual([]); // boss filter + a query that matches nothing
		// …yet the projection was memoized: the same array reference, never rebuilt.
		expect(projection).toBe(projectionBefore);
		expect(view.enemiesTab.enemyProjections).toBe(projectionBefore);

		cleanup();
	});
});

describe('CodexView enemy search', () => {
	it('matches by name, case-insensitively', () => {
		const view = new CodexView();
		view.enemiesTab.search = 'BOG';
		expect(view.enemiesTab.enemyRows.map((r) => r.id)).toEqual([1]);
	});

	it('matches by zone name (spawn zones + a boss encounter zone)', () => {
		const view = new CodexView();
		// Dust Skitterer spawns in Emberreach; Cinder Tyrant's boss encounter is in Emberreach too.
		view.enemiesTab.search = 'emberreach';
		expect(view.enemiesTab.enemyRows.map((r) => r.id)).toEqual([0, 2]);
	});

	it('matches a boss by its encounter-zone name', () => {
		const view = new CodexView();
		view.enemiesTab.setFilter('boss');
		view.enemiesTab.search = 'emberreach'; // Cinder Tyrant's encounter zone (it has no spawns)
		expect(view.enemiesTab.enemyRows.map((r) => r.id)).toEqual([2]);
	});

	it('matches the boss kind', () => {
		const view = new CodexView();
		view.enemiesTab.search = 'boss';
		expect(view.enemiesTab.enemyRows.map((r) => r.id)).toEqual([2]);
	});

	it('shows everything for an empty query', () => {
		const view = new CodexView();
		view.enemiesTab.search = '   ';
		expect(view.enemiesTab.enemyRows).toHaveLength(3);
	});

	it('shows nothing when the query matches no enemy', () => {
		const view = new CodexView();
		view.enemiesTab.search = 'griffin';
		expect(view.enemiesTab.enemyRows).toHaveLength(0);
		expect(view.enemiesTab.shownCount).toBe(0);
	});

	it('reflects the search in the shown count and combines with the filter', () => {
		const view = new CodexView();
		view.enemiesTab.setFilter('normal');
		view.enemiesTab.search = 'lurker';
		expect(view.enemiesTab.enemyRows.map((r) => r.id)).toEqual([1]);
		expect(view.enemiesTab.shownCount).toBe(1);
	});

	it('keeps an explicitly selected enemy in the dossier even when the search excludes it', () => {
		// Mirrors the filter behavior: a deliberate selection stays resolvable so the dossier
		// doesn't jump out from under the player when they type a query.
		const view = new CodexView();
		view.enemiesTab.selectEnemy(0); // explicitly inspecting Dust Skitterer
		view.enemiesTab.search = 'bog'; // the table shows only Bog Lurker…
		expect(view.enemiesTab.enemyRows.map((r) => r.id)).toEqual([1]);
		expect(view.enemiesTab.selectedEnemy?.id).toBe(0); // …but the dossier holds the selection
	});

	it('falls back the dossier to the first visible row when nothing is explicitly selected', () => {
		const view = new CodexView();
		view.enemiesTab.selectedEnemyId = -1; // no resolvable selection
		view.enemiesTab.search = 'bog'; // only Bog Lurker is visible
		expect(view.enemiesTab.selectedEnemy?.id).toBe(1);
	});
});

describe('CodexView enemy sort', () => {
	it('defaults to ascending level (boss fixed level ranks among normals)', () => {
		// Dust Skitterer (1) < Cinder Tyrant (boss, 10) < Bog Lurker (11).
		expect(new CodexView().enemiesTab.enemyRows.map((r) => r.id)).toEqual([0, 2, 1]);
	});

	it('sorts alphabetically by name', () => {
		const view = new CodexView();
		view.enemiesTab.sort = 'name';
		// Bog Lurker, Cinder Tyrant, Dust Skitterer.
		expect(view.enemiesTab.enemyRows.map((r) => r.id)).toEqual([1, 2, 0]);
	});
});

describe('CodexView selection', () => {
	it('falls back to the head of the list when no enemy is selected', () => {
		expect(new CodexView().enemiesTab.selectedEnemy?.id).toBe(0);
	});

	it('seeds the level to the band midpoint for a normal enemy', () => {
		// Dust Skitterer spans 1–22 → midpoint 12.
		expect(new CodexView().enemiesTab.level).toBe(12);
	});

	it('selectEnemy reseeds the level (fixed for a boss) and resets the sub-tab', () => {
		const view = new CodexView();
		view.enemiesTab.selectSub('skills');
		view.enemiesTab.selectEnemy(2);
		expect(view.enemiesTab.selectedEnemy?.id).toBe(2);
		expect(view.enemiesTab.level).toBe(10); // boss fixed level
		expect(view.enemiesTab.sub).toBe('attributes');
		expect(view.enemiesTab.range?.fixed).toBe(true);
	});
});

describe('CodexView dossier projections', () => {
	it('includes the Challenges sub-tab only when the enemy has related challenges', () => {
		const view = new CodexView();
		view.enemiesTab.selectEnemy(0); // has challenge 0
		expect(view.enemiesTab.subTabs.map((t) => t.key)).toContain('challenges');
		view.enemiesTab.selectEnemy(1); // no challenges
		expect(view.enemiesTab.subTabs.map((t) => t.key)).not.toContain('challenges');
	});

	it('builds enemy-scoped challenge rows with progress text', () => {
		const view = new CodexView();
		view.enemiesTab.selectEnemy(0);
		expect(view.enemiesTab.challenges).toEqual([
			expect.objectContaining({ id: 0, typeLabel: 'Enemies Killed', progressText: '62/100', completed: false })
		]);
		// A goal-of-1 boss challenge with no progress reads as "sealed".
		view.enemiesTab.selectEnemy(2);
		expect(view.enemiesTab.challenges[0].progressText).toBe('sealed');
	});

	it('hides a retired enemy-scoped challenge unless it was already completed', () => {
		staticData.challenges = [
			...staticData.challenges,
			{
				id: 2,
				name: 'Old Bounty',
				challengeTypeId: EChallengeType.EnemiesKilled,
				entityType: EEntityType.Enemy,
				targetEntityId: 0,
				progressGoal: 50,
				retiredAt: '2026-01-01T00:00:00Z'
			}
		];
		const uncompleted = new CodexView();
		uncompleted.enemiesTab.selectEnemy(0);
		expect(uncompleted.enemiesTab.challenges.map((c) => c.id)).toEqual([0]);

		playerChallenges.all = [...playerChallenges.all, { challengeId: 2, progress: 50, completed: true }];
		const completed = new CodexView();
		completed.enemiesTab.selectEnemy(0);
		expect(completed.enemiesTab.challenges.map((c) => c.id)).toEqual(expect.arrayContaining([0, 2]));
	});

	it('sorts spawn shares descending and collapses a boss to a single Encounter', () => {
		const view = new CodexView();
		view.enemiesTab.selectEnemy(0);
		expect(view.enemiesTab.spawnHeading).toBe('Spawns in 2 zones');
		expect(view.enemiesTab.spawns).toEqual([
			expect.objectContaining({ zoneName: 'Emberreach', share: 100 }),
			expect.objectContaining({ zoneName: 'Ashfen Marsh', share: 33 })
		]);
		view.enemiesTab.selectEnemy(2);
		expect(view.enemiesTab.spawnHeading).toBe('Encounter');
		expect(view.enemiesTab.spawns).toEqual([
			expect.objectContaining({ zoneName: 'Emberreach', share: 100, weightLabel: 'boss fight' })
		]);
	});

	it('lists the enemy’s skill pool with base/cooldown meta', () => {
		const view = new CodexView();
		view.enemiesTab.selectEnemy(0);
		expect(view.enemiesTab.enemySkillRows).toEqual([
			{ id: 0, name: 'Cleave', meta: 'base 14 · 1.8s cd' },
			{ id: 1, name: 'War Cry', meta: 'utility · 6s cd' }
		]);
	});

	it('reuses the statistics query for the player’s per-enemy record', () => {
		statistics.stats = STATS;
		const view = new CodexView();
		view.enemiesTab.selectEnemy(0);
		const killed = view.enemiesTab.statistics.find((s) => s.label === 'Enemies Killed');
		expect(killed?.value).toBe('100');
	});

	it('scales primary attributes to the current level', () => {
		const view = new CodexView();
		view.enemiesTab.selectEnemy(0); // level → 12
		const str = view.enemiesTab.attributes.primary.find((p) => p.attributeId === EAttribute.Strength);
		expect(str?.value).toBe(Math.round(10 + 1 * 12)); // 22
		expect(view.enemiesTab.attributes.secondary.map((s) => s.attributeId)).toEqual([
			EAttribute.MaxHealth,
			EAttribute.Toughness
		]);
	});
});

describe('CodexView zone rail', () => {
	it('projects status / band / spawn-pool / boss per zone in authored order', () => {
		const rows = new CodexView().zonesTab.zoneRows;
		expect(rows.map((r) => r.id)).toEqual([0, 1, 2]);
		// Zone 0 is cleared; zone 1 is gated on the uncompleted challenge 0; zone 2 is open.
		expect(rows.find((r) => r.id === 0)).toMatchObject({
			status: 'cleared',
			band: '1–10',
			spawnCount: 1,
			hasBoss: true
		});
		expect(rows.find((r) => r.id === 1)).toMatchObject({
			status: 'locked',
			band: '11–22',
			spawnCount: 2,
			hasBoss: false
		});
		expect(rows.find((r) => r.id === 2)).toMatchObject({
			status: 'unlocked',
			band: '18–28',
			spawnCount: 0,
			hasBoss: false
		});
	});

	it('marks a gated zone unlocked once its challenge is completed', () => {
		playerChallenges.all = [{ challengeId: 0, progress: 100, completed: true }];
		expect(new CodexView().zonesTab.zoneRows.find((r) => r.id === 1)?.status).toBe('unlocked');
	});

	it('keeps a cleared zone cleared even behind an unmet gate', () => {
		statistics.isZoneCleared.mockImplementation((id: number) => id === 1);
		expect(new CodexView().zonesTab.zoneRows.find((r) => r.id === 1)?.status).toBe('cleared');
	});

	it('orders the rail by authored order, not by id', () => {
		// eslint-disable-next-line @typescript-eslint/no-explicit-any
		staticData.zones = staticData.zones.map((z: any) => ({ ...z, order: 3 - z.order }));
		expect(new CodexView().zonesTab.zoneRows.map((r) => r.id)).toEqual([2, 1, 0]);
	});
});

describe('CodexView zone projections', () => {
	it('projects the immutable per-zone fields without the live status, in authored order', () => {
		const rows = new CodexView().zonesTab.zoneProjections;
		expect(rows.map((r) => r.id)).toEqual([0, 1, 2]);
		expect(rows.find((r) => r.id === 1)).toEqual({
			id: 1,
			name: 'Ashfen Marsh',
			band: '11–22',
			spawnCount: 2,
			hasBoss: false
		});
		// Status is the rail's reactive overlay, not part of the immutable projection.
		expect(rows[0]).not.toHaveProperty('status');
	});

	it('memoizes the projection on a single instance: the same reference survives a zone clear / gate open', () => {
		// Asserts the perf guarantee the projection/row split exists for — not just that two fresh instances
		// agree by value. On one live instance the heavy O(zones×enemies) projection must return the *same
		// reference* when the live status the rail overlays moves; only `zoneRows` reacts. Run inside an
		// effect root so the derived stays live and its memoization (referential identity) holds.
		const view = new CodexView();
		let projection: ZoneProjectionVM[] | undefined;
		const cleanup = $effect.root(() => {
			$effect(() => {
				projection = view.zonesTab.zoneProjections;
			});
		});

		flushSync();
		const before = projection;

		// Move only the live status inputs the rail overlays (zone cleared + gate opened) — the projection
		// reads neither, so it must not be rebuilt.
		statistics.isZoneCleared.mockImplementation(() => true);
		playerChallenges.all = [{ challengeId: 0, progress: 100, completed: true }];
		flushSync();

		expect(view.zonesTab.zoneProjections).toBe(before);
		cleanup();
	});
});

describe('CodexView zone dossier', () => {
	it('falls back to the head of the rail when no zone is selected', () => {
		const view = new CodexView();
		view.zonesTab.selectedZoneId = -1;
		expect(view.zonesTab.selectedZone?.id).toBe(0);
	});

	it('resolves the zone boss card (enemy + fixed level), null when none is authored', () => {
		const view = new CodexView();
		view.zonesTab.selectZone(0);
		expect(view.zonesTab.zoneBoss).toEqual({ enemyId: 2, name: 'Cinder Tyrant', level: 10 });
		view.zonesTab.selectZone(1);
		expect(view.zonesTab.zoneBoss).toBeNull();
	});

	it('builds the spawn table ordered by share with weight labels', () => {
		const view = new CodexView();
		view.zonesTab.selectZone(1); // Bog Lurker (40) + Dust Skitterer (20) → 67% / 33%
		expect(view.zonesTab.zoneSpawns).toEqual([
			expect.objectContaining({ enemyId: 1, enemyName: 'Bog Lurker', share: 67, weightLabel: 'weight 40' }),
			expect.objectContaining({ enemyId: 0, enemyName: 'Dust Skitterer', share: 33, weightLabel: 'weight 20' })
		]);
		expect(view.zonesTab.zoneSpawnCount).toBe(2);
	});

	it('reports the unlock condition (sealed when locked), null for an always-open zone', () => {
		const view = new CodexView();
		view.zonesTab.selectZone(1);
		expect(view.zonesTab.selectedZoneStatus).toBe('locked');
		expect(view.zonesTab.zoneUnlock).toMatchObject({
			challengeId: 0,
			challengeName: 'Cull the Skitterers',
			sealed: true
		});
		view.zonesTab.selectZone(2);
		expect(view.zonesTab.zoneUnlock).toBeNull();
		expect(view.zonesTab.zoneSpawns).toEqual([]);
	});

	it('unseals the unlock condition once the gating challenge completes', () => {
		playerChallenges.all = [{ challengeId: 0, progress: 100, completed: true }];
		const view = new CodexView();
		view.zonesTab.selectZone(1);
		expect(view.zonesTab.selectedZoneStatus).toBe('unlocked');
		expect(view.zonesTab.zoneUnlock?.sealed).toBe(false);
	});

	it('reuses the statistics query for the player’s per-zone record', () => {
		statistics.stats = STATS;
		const view = new CodexView();
		view.zonesTab.selectZone(1); // Ashfen Marsh — 3 clears recorded
		expect(view.zonesTab.zoneStatistics.find((s) => s.label === 'Zones Cleared')?.value).toBe('3');
		// A zone with no recorded statistics yields an empty record.
		view.zonesTab.selectZone(2);
		expect(view.zonesTab.zoneStatistics).toEqual([]);
	});
});

describe('CodexView zone → enemy cross-link', () => {
	it('openEnemy switches to the Enemies tab and selects the enemy', () => {
		const view = new CodexView();
		view.selectTab('zones');
		view.openEnemy(2);
		expect(view.tab).toBe('enemies');
		expect(view.enemiesTab.selectedEnemyId).toBe(2);
		expect(view.enemiesTab.sub).toBe('attributes'); // selectEnemy resets the sub-tab
		expect(view.enemiesTab.level).toBe(10); // boss fixed level
	});
});

describe('CodexView enemy → zone cross-link', () => {
	it('openZone switches to the Zones tab and selects the zone', () => {
		const view = new CodexView();
		view.enemiesTab.selectEnemy(0); // a normal enemy with spawn zones
		view.openZone(1);
		expect(view.tab).toBe('zones');
		expect(view.zonesTab.selectedZoneId).toBe(1);
		expect(view.zonesTab.selectedZone?.id).toBe(1);
	});
});

describe('CodexView enemy → skill cross-link', () => {
	it('openSkill switches to the Skills tab and selects the skill', () => {
		const view = new CodexView();
		view.enemiesTab.selectEnemy(0); // skill pool [0, 1]
		view.openSkill(1);
		expect(view.tab).toBe('skills');
		expect(view.skillsTab.selectedSkillId).toBe(1);
		expect(view.skillsTab.selectedSkill?.id).toBe(1);
	});
});

describe('CodexView skill table', () => {
	it('lists every skill in catalogue order with base/cooldown/used-by projections', () => {
		const rows = new CodexView().skillsTab.skillRows;
		expect(rows.map((r) => r.id)).toEqual([0, 1, 2]);
		// Cleave is a 14-dmg attack used by Dust Skitterer + the boss; War Cry is utility used by all
		// three; Focus is player-only (used by none) and still appears.
		expect(rows.find((r) => r.id === 0)).toMatchObject({
			name: 'Cleave',
			baseDamageLabel: '14',
			cooldownLabel: '1.8s',
			usedByCount: 2
		});
		expect(rows.find((r) => r.id === 1)).toMatchObject({ baseDamageLabel: '—', cooldownLabel: '6s', usedByCount: 3 });
		expect(rows.find((r) => r.id === 2)).toMatchObject({ name: 'Focus', cooldownLabel: '—', usedByCount: 0 });
	});

	it('tracks the selected skill (the row highlight reads selectedSkillId, kept out of the projection)', () => {
		const view = new CodexView();
		view.skillsTab.selectSkill(1);
		expect(view.skillsTab.selectedSkillId).toBe(1);
		expect(view.skillsTab.selectedSkill?.id).toBe(1);
	});

	it('tints each row by its rarity tier and exposes the selected skill rarity for the dossier', () => {
		const view = new CodexView();
		// Cleave is authored Rare; its row mark and the dossier accent resolve to the rarity hue + label.
		expect(view.skillsTab.skillRows.find((r) => r.id === 0)?.rarityColor).toContain('--rarity-');
		view.skillsTab.selectSkill(0);
		expect(view.skillsTab.selectedSkillRarity).toEqual({ color: expect.stringContaining('--rarity-'), label: 'Rare' });
	});
});

describe('CodexView skill dossier', () => {
	it('defaults the selection to the head of the catalogue', () => {
		expect(new CodexView().skillsTab.selectedSkill?.id).toBe(0);
	});

	it('selectSkill changes the inspected skill', () => {
		const view = new CodexView();
		view.skillsTab.selectSkill(2);
		expect(view.skillsTab.selectedSkill?.id).toBe(2);
	});

	it('projects the damage-scaling attributes tinted by their accent', () => {
		const view = new CodexView();
		view.skillsTab.selectSkill(0); // Cleave scales ×1.5 Strength
		expect(view.skillsTab.skillScaling).toEqual([
			{
				attributeId: EAttribute.Strength,
				name: 'Strength',
				code: 'STR',
				multiplierLabel: '×1.5',
				color: 'var(--attr-strength)'
			}
		]);
		// War Cry has no damage scaling.
		view.skillsTab.selectSkill(1);
		expect(view.skillsTab.skillScaling).toEqual([]);
	});

	it('describes effects via the shared helper, classifying buff vs debuff', () => {
		const view = new CodexView();
		view.skillsTab.selectSkill(1); // War Cry: +15 STR (self, buff) and ×0.5 Toughness (enemy, debuff)
		expect(view.skillsTab.skillEffects).toEqual([
			{
				id: 0,
				magnitude: '+15',
				attributeName: 'Strength',
				targetLabel: 'self',
				duration: '5s',
				color: 'var(--effect-buff)'
			},
			{
				id: 1,
				magnitude: '×0.5',
				attributeName: 'Toughness',
				targetLabel: 'enemy',
				duration: '4s',
				color: 'var(--effect-debuff)'
			}
		]);
		// Cleave applies no effects.
		view.skillsTab.selectSkill(0);
		expect(view.skillsTab.skillEffects).toEqual([]);
	});

	it('lists the enemies that use the skill, flagging bosses, and is empty for a player-only skill', () => {
		const view = new CodexView();
		view.skillsTab.selectSkill(0); // Cleave — Dust Skitterer (normal) + Cinder Tyrant (boss)
		expect(view.skillsTab.skillUsedBy).toEqual([
			{ enemyId: 0, name: 'Dust Skitterer', isBoss: false, accent: 'var(--enemy-accent)' },
			{ enemyId: 2, name: 'Cinder Tyrant', isBoss: true, accent: 'var(--boss-accent)' }
		]);
		view.skillsTab.selectSkill(2); // Focus — player-only
		expect(view.skillsTab.skillUsedBy).toEqual([]);
	});

	it('reuses the statistics query for the player’s per-skill record', () => {
		statistics.stats = STATS;
		const view = new CodexView();
		view.skillsTab.selectSkill(0); // Cleave — 4,500 damage dealt recorded
		expect(view.skillsTab.skillStatistics.find((s) => s.label === 'Damage Dealt')?.value).toBe('4,500');
		// A skill with no recorded statistics yields an empty record.
		view.skillsTab.selectSkill(2);
		expect(view.skillsTab.skillStatistics).toEqual([]);
	});

	it('surfaces an item grant as a "Granted by" source, tinted by the item rarity', () => {
		const view = new CodexView();
		view.skillsTab.selectSkill(1); // War Cry — granted by the Rare Ember Staff
		expect(view.skillsTab.skillProvenance.status).toBe('obtainable');
		expect(view.skillsTab.skillProvenance.sources).toEqual([
			{ kind: 'item', id: 0, label: 'Granted by', name: 'Ember Staff', accent: 'var(--rarity-rare)' }
		]);
	});

	it('words an enemy-flagged skill with no player source as enemy-only', () => {
		const view = new CodexView();
		view.skillsTab.selectSkill(2); // Focus — Enemy-flagged, no reward/grant
		expect(view.skillsTab.skillProvenance.status).toBe('enemy-only');
		expect(view.skillsTab.skillProvenance.sources).toEqual([]);
		expect(view.skillsTab.skillProvenance.emptyLabel).toContain('Enemy-only');
	});

	it('words a flagged-but-unreferenced skill as not obtainable (intent ≠ reality)', () => {
		// Re-flag Focus as Item-acquirable but leave it granted by no item: the flag must not invent a source.
		// eslint-disable-next-line @typescript-eslint/no-explicit-any
		staticData.skills[2] = { ...(staticData.skills[2] as any), acquisition: ESkillAcquisition.Item };
		const view = new CodexView();
		view.skillsTab.selectSkill(2);
		expect(view.skillsTab.skillProvenance.status).toBe('unobtainable');
		expect(view.skillsTab.skillProvenance.sources).toEqual([]);
		expect(view.skillsTab.skillProvenance.emptyLabel).toBe('Not currently obtainable.');
	});

	it('excludes a retired item grant from the sources', () => {
		// Retire the Ember Staff: War Cry loses its only source and falls back to its flag wording.
		// eslint-disable-next-line @typescript-eslint/no-explicit-any
		staticData.items[0] = { ...(staticData.items[0] as any), retiredAt: '2026-01-01T00:00:00Z' };
		const view = new CodexView();
		view.skillsTab.selectSkill(1); // War Cry — Item-flagged, but its granting item is retired
		expect(view.skillsTab.skillProvenance.sources).toEqual([]);
		expect(view.skillsTab.skillProvenance.status).toBe('unobtainable');
	});
});

describe('CodexView skill → enemy cross-link', () => {
	it('openEnemy from a used-by pill switches to the Enemies tab and selects the enemy', () => {
		const view = new CodexView();
		view.selectTab('skills');
		view.openEnemy(2);
		expect(view.tab).toBe('enemies');
		expect(view.enemiesTab.selectedEnemyId).toBe(2);
	});
});

describe('CodexView retired-record resolution', () => {
	// Retirement keeps a record's slot resolvable by id (backend.md → _Reference Data_); an explicitly
	// requested retired id must resolve to the actual record rather than silently falling back to the
	// head of the live list.
	it('resolves a deep-linked retired enemy id instead of falling back to the head of the list', () => {
		staticData.enemies[1] = { ...staticData.enemies[1], retiredAt: '2026-01-01T00:00:00Z' };
		const view = new CodexView({ tab: 'enemies', enemyId: 1 });
		expect(view.enemiesTab.selectedEnemy?.id).toBe(1);
	});

	it('selectEnemy resolves a retired id (a cross-link into a retired record)', () => {
		staticData.enemies[1] = { ...staticData.enemies[1], retiredAt: '2026-01-01T00:00:00Z' };
		const view = new CodexView();
		view.enemiesTab.selectEnemy(1);
		expect(view.enemiesTab.selectedEnemy?.id).toBe(1);
	});

	it('resolves a deep-linked retired zone id', () => {
		staticData.zones[1] = { ...staticData.zones[1], retiredAt: '2026-01-01T00:00:00Z' };
		const view = new CodexView({ tab: 'zones', zoneId: 1 });
		expect(view.zonesTab.selectedZone?.id).toBe(1);
	});

	it('selectZone resolves a retired id', () => {
		staticData.zones[1] = { ...staticData.zones[1], retiredAt: '2026-01-01T00:00:00Z' };
		const view = new CodexView();
		view.zonesTab.selectZone(1);
		expect(view.zonesTab.selectedZone?.id).toBe(1);
	});

	it('resolves a deep-linked retired skill id', () => {
		staticData.skills[1] = { ...staticData.skills[1], retiredAt: '2026-01-01T00:00:00Z' };
		const view = new CodexView({ tab: 'skills', skillId: 1 });
		expect(view.skillsTab.selectedSkill?.id).toBe(1);
	});

	it('selectSkill resolves a retired id', () => {
		staticData.skills[1] = { ...staticData.skills[1], retiredAt: '2026-01-01T00:00:00Z' };
		const view = new CodexView();
		view.skillsTab.selectSkill(1);
		expect(view.skillsTab.selectedSkill?.id).toBe(1);
	});
});

describe('CodexView nav payload', () => {
	it('seeds tab / enemy / sub-tab / level from a deep-link payload', () => {
		const view = new CodexView({ tab: 'enemies', enemyId: 2, sub: 'statistics' });
		expect(view.tab).toBe('enemies');
		expect(view.enemiesTab.selectedEnemyId).toBe(2);
		expect(view.enemiesTab.sub).toBe('statistics');
		expect(view.enemiesTab.level).toBe(10); // boss fixed level
	});

	it('seeds the tab + zone from a deep-link payload', () => {
		const view = new CodexView({ tab: 'zones', zoneId: 2 });
		expect(view.tab).toBe('zones');
		expect(view.zonesTab.selectedZoneId).toBe(2);
	});

	it('seeds the tab + skill from a deep-link payload', () => {
		const view = new CodexView({ tab: 'skills', skillId: 2 });
		expect(view.tab).toBe('skills');
		expect(view.skillsTab.selectedSkillId).toBe(2);
	});
});
