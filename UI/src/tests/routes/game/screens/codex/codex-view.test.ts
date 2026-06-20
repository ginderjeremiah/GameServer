import { describe, it, expect, beforeEach, vi } from 'vitest';
import { EAttribute, EChallengeType, EEntityType, EStatisticType, type IPlayerStatistic } from '$lib/api';
import { SERVER_STAT_TYPES } from '../stats/stat-fixtures';

// CodexView reads reference data + challenge progress from the stores, and reuses the Statistics
// screen's query engine — all mocked here. (The Statistics view-model also imports `navigation`.)
const { staticData, playerChallenges, navigation } = vi.hoisted(() => ({
	// eslint-disable-next-line @typescript-eslint/no-explicit-any
	staticData: {} as any,
	// eslint-disable-next-line @typescript-eslint/no-explicit-any
	playerChallenges: { all: [] as any[] },
	navigation: { requestScreen: vi.fn(), consumePayload: vi.fn(), clear: vi.fn() }
}));
vi.mock('$stores', () => ({ staticData, playerChallenges, navigation }));

import { CodexView } from '$routes/game/screens/codex/codex-view.svelte';

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
		{ id: 0, name: 'Emberreach', order: 1, levelMin: 1, levelMax: 10, bossEnemyId: 2, bossLevel: 10 },
		{ id: 1, name: 'Ashfen Marsh', order: 2, levelMin: 11, levelMax: 22, bossLevel: 22 },
		{ id: 2, name: 'Sunken Causeway', order: 3, levelMin: 18, levelMax: 28, bossLevel: 28 }
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
		{ id: 0, name: 'Cleave', baseDamage: 14, cooldownMs: 1800, damageMultipliers: [], effects: [] },
		{ id: 1, name: 'War Cry', baseDamage: 0, cooldownMs: 6000, damageMultipliers: [], effects: [] }
	];
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
	staticData.attributes = [];
	playerChallenges.all = [{ challengeId: 0, progress: 62, completed: false }];
}

const STATS: IPlayerStatistic[] = [
	{ statisticTypeId: EStatisticType.EnemiesKilled, entityId: 0, value: 100 },
	{ statisticTypeId: EStatisticType.EnemiesKilled, entityId: 1, value: 20 }
];

beforeEach(() => {
	seed();
	navigation.requestScreen.mockClear();
});

describe('CodexView tabs', () => {
	it('reports live counts per section', () => {
		const tabs = new CodexView().tabs;
		expect(tabs.map((t) => [t.key, t.count])).toEqual([
			['enemies', 3],
			['zones', 3],
			['skills', 2]
		]);
	});
});

describe('CodexView enemy rows', () => {
	it('projects zone + skill counts (boss shows a single zone)', () => {
		const rows = new CodexView().enemyRows;
		expect(rows.find((r) => r.id === 0)).toMatchObject({ zoneCount: 2, skillCount: 2, band: '1–22' });
		expect(rows.find((r) => r.id === 2)).toMatchObject({ zoneCount: 1, skillCount: 2, band: 'L10', isBoss: true });
	});

	it('filters by normal / boss', () => {
		const view = new CodexView();
		view.setFilter('boss');
		expect(view.enemyRows.map((r) => r.id)).toEqual([2]);
		view.setFilter('normal');
		expect(view.enemyRows.map((r) => r.id)).toEqual([0, 1]);
	});

	it('exposes the level sort key + search haystack on each row', () => {
		const rows = new CodexView().enemyRows;
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

describe('CodexView enemy search', () => {
	it('matches by name, case-insensitively', () => {
		const view = new CodexView();
		view.search = 'BOG';
		expect(view.enemyRows.map((r) => r.id)).toEqual([1]);
	});

	it('matches by zone name (spawn zones + a boss encounter zone)', () => {
		const view = new CodexView();
		// Dust Skitterer spawns in Emberreach; Cinder Tyrant's boss encounter is in Emberreach too.
		view.search = 'emberreach';
		expect(view.enemyRows.map((r) => r.id)).toEqual([0, 2]);
	});

	it('matches a boss by its encounter-zone name', () => {
		const view = new CodexView();
		view.setFilter('boss');
		view.search = 'emberreach'; // Cinder Tyrant's encounter zone (it has no spawns)
		expect(view.enemyRows.map((r) => r.id)).toEqual([2]);
	});

	it('matches the boss kind', () => {
		const view = new CodexView();
		view.search = 'boss';
		expect(view.enemyRows.map((r) => r.id)).toEqual([2]);
	});

	it('shows everything for an empty query', () => {
		const view = new CodexView();
		view.search = '   ';
		expect(view.enemyRows).toHaveLength(3);
	});

	it('shows nothing when the query matches no enemy', () => {
		const view = new CodexView();
		view.search = 'griffin';
		expect(view.enemyRows).toHaveLength(0);
		expect(view.shownCount).toBe(0);
	});

	it('reflects the search in the shown count and combines with the filter', () => {
		const view = new CodexView();
		view.setFilter('normal');
		view.search = 'lurker';
		expect(view.enemyRows.map((r) => r.id)).toEqual([1]);
		expect(view.shownCount).toBe(1);
	});

	it('keeps an explicitly selected enemy in the dossier even when the search excludes it', () => {
		// Mirrors the filter behavior: a deliberate selection stays resolvable so the dossier
		// doesn't jump out from under the player when they type a query.
		const view = new CodexView();
		view.selectEnemy(0); // explicitly inspecting Dust Skitterer
		view.search = 'bog'; // the table shows only Bog Lurker…
		expect(view.enemyRows.map((r) => r.id)).toEqual([1]);
		expect(view.selectedEnemy?.id).toBe(0); // …but the dossier holds the selection
	});

	it('falls back the dossier to the first visible row when nothing is explicitly selected', () => {
		const view = new CodexView();
		view.selectedEnemyId = -1; // no resolvable selection
		view.search = 'bog'; // only Bog Lurker is visible
		expect(view.selectedEnemy?.id).toBe(1);
	});
});

describe('CodexView enemy sort', () => {
	it('defaults to ascending level (boss fixed level ranks among normals)', () => {
		// Dust Skitterer (1) < Cinder Tyrant (boss, 10) < Bog Lurker (11).
		expect(new CodexView().enemyRows.map((r) => r.id)).toEqual([0, 2, 1]);
	});

	it('sorts alphabetically by name', () => {
		const view = new CodexView();
		view.sort = 'name';
		// Bog Lurker, Cinder Tyrant, Dust Skitterer.
		expect(view.enemyRows.map((r) => r.id)).toEqual([1, 2, 0]);
	});
});

describe('CodexView selection', () => {
	it('falls back to the head of the list when no enemy is selected', () => {
		expect(new CodexView().selectedEnemy?.id).toBe(0);
	});

	it('seeds the level to the band midpoint for a normal enemy', () => {
		// Dust Skitterer spans 1–22 → midpoint 12.
		expect(new CodexView().level).toBe(12);
	});

	it('selectEnemy reseeds the level (fixed for a boss) and resets the sub-tab', () => {
		const view = new CodexView();
		view.selectSub('skills');
		view.selectEnemy(2);
		expect(view.selectedEnemy?.id).toBe(2);
		expect(view.level).toBe(10); // boss fixed level
		expect(view.sub).toBe('attributes');
		expect(view.range?.fixed).toBe(true);
	});
});

describe('CodexView dossier projections', () => {
	it('includes the Challenges sub-tab only when the enemy has related challenges', () => {
		const view = new CodexView();
		view.selectEnemy(0); // has challenge 0
		expect(view.subTabs.map((t) => t.key)).toContain('challenges');
		view.selectEnemy(1); // no challenges
		expect(view.subTabs.map((t) => t.key)).not.toContain('challenges');
	});

	it('builds enemy-scoped challenge rows with progress text', () => {
		const view = new CodexView();
		view.selectEnemy(0);
		expect(view.challenges).toEqual([
			expect.objectContaining({ id: 0, typeLabel: 'Enemies Killed', progressText: '62/100', completed: false })
		]);
		// A goal-of-1 boss challenge with no progress reads as "sealed".
		view.selectEnemy(2);
		expect(view.challenges[0].progressText).toBe('sealed');
	});

	it('sorts spawn shares descending and collapses a boss to a single Encounter', () => {
		const view = new CodexView();
		view.selectEnemy(0);
		expect(view.spawnHeading).toBe('Spawns in 2 zones');
		expect(view.spawns).toEqual([
			expect.objectContaining({ zoneName: 'Emberreach', share: 100 }),
			expect.objectContaining({ zoneName: 'Ashfen Marsh', share: 33 })
		]);
		view.selectEnemy(2);
		expect(view.spawnHeading).toBe('Encounter');
		expect(view.spawns).toEqual([
			expect.objectContaining({ zoneName: 'Emberreach', share: 100, weightLabel: 'boss fight' })
		]);
	});

	it('lists the enemy’s skill pool with base/cooldown meta', () => {
		const view = new CodexView();
		view.selectEnemy(0);
		expect(view.skillRows).toEqual([
			{ id: 0, name: 'Cleave', meta: 'base 14 · 1.8s cd' },
			{ id: 1, name: 'War Cry', meta: 'utility · 6s cd' }
		]);
	});

	it('reuses the statistics query for the player’s per-enemy record', () => {
		const view = new CodexView();
		view.stats = STATS;
		view.selectEnemy(0);
		const killed = view.statistics.find((s) => s.label === 'Enemies Killed');
		expect(killed?.value).toBe('100');
	});

	it('scales primary attributes to the current level', () => {
		const view = new CodexView();
		view.selectEnemy(0); // level → 12
		const str = view.attributes.primary.find((p) => p.attributeId === EAttribute.Strength);
		expect(str?.value).toBe(Math.round(10 + 1 * 12)); // 22
		expect(view.attributes.secondary.map((s) => s.attributeId)).toEqual([EAttribute.MaxHealth, EAttribute.Defense]);
	});
});

describe('CodexView nav payload', () => {
	it('seeds tab / enemy / sub-tab / level from a deep-link payload', () => {
		const view = new CodexView({ tab: 'enemies', enemyId: 2, sub: 'statistics' });
		expect(view.tab).toBe('enemies');
		expect(view.selectedEnemyId).toBe(2);
		expect(view.sub).toBe('statistics');
		expect(view.level).toBe(10); // boss fixed level
	});

	it('seeds the zone tab + zone from a deep-link payload', () => {
		const view = new CodexView({ tab: 'zones', zoneId: 2 });
		expect(view.tab).toBe('zones');
		expect(view.selectedZoneId).toBe(2);
	});
});

describe('CodexView zone rail', () => {
	it('lists non-retired zones in authored order with level band + spawn-pool count', () => {
		const view = new CodexView();
		// Dust Skitterer spawns in zones 0 + 1; Bog Lurker in zone 1; nothing in zone 2.
		expect(view.zoneRows.map((z) => [z.id, z.band, z.spawnCount])).toEqual([
			[0, '1–10', 1],
			[1, '11–22', 2],
			[2, '18–28', 0]
		]);
	});

	it('marks an ungated zone unlocked, and cleared once its ZonesCleared stat is set', () => {
		const view = new CodexView();
		view.stats = [{ statisticTypeId: EStatisticType.ZonesCleared, entityId: 0, value: 1 }];
		expect(view.zoneRows.find((z) => z.id === 0)?.status).toBe('cleared');
		expect(view.zoneRows.find((z) => z.id === 1)?.status).toBe('unlocked');
	});

	it('locks a zone whose gating challenge is incomplete, unlocking it once completed', () => {
		staticData.zones[2].unlockChallengeId = 1; // gate zone 2 on challenge 1
		expect(new CodexView().zoneRows.find((z) => z.id === 2)?.status).toBe('locked');
		playerChallenges.all = [{ challengeId: 1, progress: 1, completed: true }];
		expect(new CodexView().zoneRows.find((z) => z.id === 2)?.status).toBe('unlocked');
	});
});

describe('CodexView zone dossier', () => {
	it('falls back to the head of the rail and exposes the band + boss card', () => {
		const view = new CodexView();
		expect(view.selectedZone?.id).toBe(0);
		expect(view.selectedZoneBand).toBe('1–10');
		expect(view.zoneBoss).toEqual({ id: 2, name: 'Cinder Tyrant', level: 10 });
	});

	it('has no boss card for a bossless zone', () => {
		const view = new CodexView();
		view.selectZone(1);
		expect(view.zoneBoss).toBeUndefined();
	});

	it('builds the spawn table ordered by share, resolving enemy names', () => {
		const view = new CodexView();
		view.selectZone(1); // Dust Skitterer (20) + Bog Lurker (40) → total 60
		expect(view.zoneSpawnRows).toEqual([
			expect.objectContaining({ enemyId: 1, enemyName: 'Bog Lurker', share: 67 }),
			expect.objectContaining({ enemyId: 0, enemyName: 'Dust Skitterer', share: 33 })
		]);
	});

	it('reports the unlock gate (open by default, sealed name while locked)', () => {
		expect(new CodexView().zoneUnlock).toEqual({ gated: false, challengeName: '', locked: false });
		staticData.zones[0].unlockChallengeId = 1; // gate zone 0 on the (incomplete) challenge 1
		const gated = new CodexView();
		gated.selectZone(0);
		expect(gated.zoneUnlock).toEqual({ gated: true, challengeName: 'The Tyrant Falls', locked: true });
	});

	it('cross-links a zone boss/spawn into the enemy dossier on the Enemies tab', () => {
		const view = new CodexView();
		view.selectTab('zones');
		view.openEnemy(2);
		expect(view.tab).toBe('enemies');
		expect(view.selectedEnemy?.id).toBe(2);
		expect(view.sub).toBe('attributes');
	});
});
