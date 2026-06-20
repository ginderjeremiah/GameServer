import { describe, it, expect, beforeEach, vi } from 'vitest';
import { EEntityType, EStatisticType, type IPlayerStatistic } from '$lib/api';

// StatisticsView reads the statistic-type catalogue + entity lists from the in-memory staticData
// store, and deep-links enemies into the Codex via the navigation store — both mocked here.
const { staticData, navigation } = vi.hoisted(() => ({
	// eslint-disable-next-line @typescript-eslint/no-explicit-any
	staticData: {} as any,
	navigation: { requestScreen: vi.fn() }
}));
vi.mock('$stores', () => ({ staticData, navigation }));

import {
	buildStatTypes,
	StatisticsData,
	StatisticsView,
	type StatEntity,
	type StatType
} from '$routes/game/screens/stats/statistics-view.svelte';
import { SERVER_STAT_TYPES } from './stat-fixtures';

const statTypes: StatType[] = buildStatTypes(SERVER_STAT_TYPES);

const entities: Record<'enemy' | 'zone' | 'skill', StatEntity[]> = {
	enemy: [
		{ id: 0, name: 'Cave Bat' },
		{ id: 1, name: 'Goblin', boss: true }
	],
	zone: [{ id: 0, name: 'Verdant Hollow', zoneNum: 1 }],
	skill: [{ id: 0, name: 'Cleave' }]
};

const stats: IPlayerStatistic[] = [
	{ statisticTypeId: EStatisticType.EnemiesKilled, entityId: 0, value: 100 },
	{ statisticTypeId: EStatisticType.EnemiesKilled, entityId: 1, value: 20 },
	{ statisticTypeId: EStatisticType.EnemiesKilled, value: 120 },
	{ statisticTypeId: EStatisticType.FastestVictory, entityId: 0, value: 2.0 },
	{ statisticTypeId: EStatisticType.FastestVictory, entityId: 1, value: 5.0 },
	{ statisticTypeId: EStatisticType.PlayerDeaths, value: 7 }
];

const data = () => new StatisticsData(statTypes, stats, entities);

/** Populates the mocked staticData with the reference data the view needs. */
function seedStaticData(): void {
	staticData.statisticTypes = SERVER_STAT_TYPES;
	staticData.enemies = [
		{ id: 0, name: 'Cave Bat', isBoss: false },
		{ id: 1, name: 'Goblin', isBoss: true }
	];
	staticData.zones = [{ id: 0, name: 'Verdant Hollow', order: 1 }];
	staticData.skills = [{ id: 0, name: 'Cleave' }];
}

/** A view seeded with the standard stats fixture (loading already finished). */
function seededView(): StatisticsView {
	const view = new StatisticsView();
	view.stats = stats;
	view.loading = false;
	return view;
}

beforeEach(() => {
	seedStaticData();
	navigation.requestScreen.mockClear();
});

describe('buildStatTypes', () => {
	it('builds one entry per presented statistic with names from the server', () => {
		expect(statTypes).toHaveLength(15);
		expect(statTypes.find((s) => s.id === EStatisticType.HighestSingleAttackDamage)!.name).toBe(
			'Highest Single Attack Damage'
		);
	});

	it('sources the entity-kind breakdown from the server entityType', () => {
		const fastest = statTypes.find((s) => s.id === EStatisticType.FastestVictory)!;
		expect(fastest.kind).toBe('enemy');
		// Presentation concerns stay on the frontend.
		expect(fastest.comp).toBe('AtMost');
		expect(fastest.agg).toBe('min');

		expect(statTypes.find((s) => s.id === EStatisticType.ZonesCleared)!.kind).toBe('zone');
		expect(statTypes.find((s) => s.id === EStatisticType.DamageDealt)!.kind).toBe('skill');
	});

	it('treats None entity types as total-only (no per-entity breakdown)', () => {
		for (const id of [EStatisticType.PlayerDeaths, EStatisticType.DamageTaken, EStatisticType.TotalBattleTime]) {
			expect(statTypes.find((s) => s.id === id)!.kind).toBe('none');
		}
	});

	it('sources the boss-only flag from the server, not a frontend hard-coding', () => {
		expect(statTypes.find((s) => s.id === EStatisticType.BossesDefeated)!.bossOnly).toBe(true);
		expect(statTypes.find((s) => s.id === EStatisticType.EnemiesKilled)!.bossOnly).toBe(false);
		// Following the server: clearing BossesDefeated's flag clears it on the built type too.
		const overridden = SERVER_STAT_TYPES.map((s) =>
			s.id === EStatisticType.BossesDefeated ? { ...s, bossOnly: false } : s
		);
		expect(buildStatTypes(overridden).find((s) => s.id === EStatisticType.BossesDefeated)!.bossOnly).toBe(false);
	});

	it('is server-driven: a changed entityType changes the kind (not hard-coded)', () => {
		// If the backend declared FastestVictory as None, the frontend follows suit.
		const overridden = SERVER_STAT_TYPES.map((s) =>
			s.id === EStatisticType.FastestVictory ? { ...s, entityType: EEntityType.None } : s
		);
		const fastest = buildStatTypes(overridden).find((s) => s.id === EStatisticType.FastestVictory)!;
		expect(fastest.kind).toBe('none');
	});

	it('skips statistics the server does not return', () => {
		const partial = SERVER_STAT_TYPES.filter((s) => s.id !== EStatisticType.SkillsUsed);
		const built = buildStatTypes(partial);
		expect(built).toHaveLength(14);
		expect(built.find((s) => s.id === EStatisticType.SkillsUsed)).toBeUndefined();
	});
});

describe('StatisticsData.rowsForStat', () => {
	it('sorts a sum/max statistic best-first (descending)', () => {
		const rows = data().rowsForStat(EStatisticType.EnemiesKilled);
		expect(rows.map((r) => r.entityId)).toEqual([0, 1]);
		expect(rows[0].entity.name).toBe('Cave Bat');
	});

	it('sorts a min statistic best-first (ascending — lower is better)', () => {
		const rows = data().rowsForStat(EStatisticType.FastestVictory);
		expect(rows.map((r) => r.value)).toEqual([2.0, 5.0]);
	});

	it('returns no rows for a total-only (none) statistic', () => {
		expect(data().rowsForStat(EStatisticType.PlayerDeaths)).toEqual([]);
	});

	it('drops rows whose entity cannot be resolved', () => {
		const d = new StatisticsData(
			statTypes,
			[{ statisticTypeId: EStatisticType.EnemiesKilled, entityId: 99, value: 5 }],
			entities
		);
		expect(d.rowsForStat(EStatisticType.EnemiesKilled)).toEqual([]);
	});
});

describe('StatisticsData.statHeadline', () => {
	it('uses the null-entity grand-total row when present', () => {
		expect(data().statHeadline(EStatisticType.EnemiesKilled)).toBe(120);
		expect(data().statHeadline(EStatisticType.PlayerDeaths)).toBe(7);
	});

	it('falls back to aggregating per-entity rows when there is no total row', () => {
		const d = new StatisticsData(
			statTypes,
			[
				{ statisticTypeId: EStatisticType.EnemiesKilled, entityId: 0, value: 100 },
				{ statisticTypeId: EStatisticType.EnemiesKilled, entityId: 1, value: 20 }
			],
			entities
		);
		expect(d.statHeadline(EStatisticType.EnemiesKilled)).toBe(120); // summed
	});
});

describe('StatisticsData.summaryFor', () => {
	it('bundles the rows, bar max, and headline for a statistic', () => {
		const summary = data().summaryFor(EStatisticType.EnemiesKilled);
		expect(summary.rows.map((r) => r.entityId)).toEqual([0, 1]);
		expect(summary.maxVal).toBe(100); // the leading row's value
		expect(summary.headline).toBe(120); // null-entity grand total
	});

	it('returns an empty (rows-less) summary for a total-only statistic', () => {
		const summary = data().summaryFor(EStatisticType.PlayerDeaths);
		expect(summary.rows).toEqual([]);
		expect(summary.maxVal).toBe(1);
		expect(summary.headline).toBe(7);
	});

	it('memoises a single summary instance per statistic', () => {
		const d = data();
		expect(d.summaryFor(EStatisticType.EnemiesKilled)).toBe(d.summaryFor(EStatisticType.EnemiesKilled));
	});
});

describe('StatisticsData.statsForEntity', () => {
	it('lists every statistic referencing an entity with its rank', () => {
		const infos = data().statsForEntity('enemy', 0);
		const killed = infos.find((i) => i.stat.id === EStatisticType.EnemiesKilled)!;
		expect(killed).toMatchObject({ value: 100, rank: 1, of: 2 });
		const fastest = infos.find((i) => i.stat.id === EStatisticType.FastestVictory)!;
		expect(fastest).toMatchObject({ value: 2.0, rank: 1, of: 2 });
	});

	it('ranks a weaker entity below its peers', () => {
		const infos = data().statsForEntity('enemy', 1);
		expect(infos.find((i) => i.stat.id === EStatisticType.EnemiesKilled)!.rank).toBe(2);
	});
});

describe('StatisticsData.isEmpty', () => {
	it('is true when there are no recorded statistics', () => {
		expect(new StatisticsData(statTypes, [], entities).isEmpty).toBe(true);
		expect(data().isEmpty).toBe(false);
	});
});

describe('StatisticsView navigation', () => {
	it('initialises to the by-statistic view on the first enemy', () => {
		const view = seededView();
		expect(view.mode).toBe('stat');
		expect(view.statCat).toBe('combat');
		expect(view.entKind).toBe('enemy');
		expect(view.entId).toBe(0);
	});

	it('filters the shown statistics by the active category', () => {
		const view = seededView();
		expect(view.shownStats.every((s) => s.cat === 'combat')).toBe(true);
		view.setStatCat('time');
		expect(view.shownStats.map((s) => s.id)).toContain(EStatisticType.FastestVictory);
	});

	it('switchKind resets the selection and query to the new kind', () => {
		const view = seededView();
		view.setQuery('bat');
		view.switchKind('skill');
		expect(view.entKind).toBe('skill');
		expect(view.entId).toBe(0);
		expect(view.query).toBe('');
	});

	it('goEntity pivots into an entity dossier', () => {
		const view = seededView();
		view.goEntity('enemy', 1);
		expect(view.mode).toBe('entity');
		expect(view.selectedEntity?.id).toBe(1);
		expect(view.entityStats.some((i) => i.stat.id === EStatisticType.EnemiesKilled)).toBe(true);
	});

	it('goStat jumps back to a statistic category', () => {
		const view = seededView();
		view.goEntity('enemy', 0);
		view.goStat('exploration');
		expect(view.mode).toBe('stat');
		expect(view.statCat).toBe('exploration');
	});

	it('openEntity deep-links an enemy into the Codex (per-entity stats live there)', () => {
		const view = seededView();
		view.openEntity('enemy', 1);
		expect(navigation.requestScreen).toHaveBeenCalledWith('codex', { tab: 'enemies', enemyId: 1, sub: 'statistics' });
		// The in-place dossier is left untouched for the enemy.
		expect(view.mode).toBe('stat');
	});

	it('openEntity opens a zone/skill in place (no Codex tab for them yet)', () => {
		const view = seededView();
		view.openEntity('zone', 0);
		expect(navigation.requestScreen).not.toHaveBeenCalled();
		expect(view.mode).toBe('entity');
		expect(view.entKind).toBe('zone');
		expect(view.entId).toBe(0);
	});

	it('filters entities by the search query', () => {
		const view = seededView();
		view.setQuery('gob');
		expect(view.filteredEntities.map((e) => e.id)).toEqual([1]);
	});

	it('keeps the resolved selection stable while the search query narrows the list', () => {
		const view = seededView();
		view.goEntity('enemy', 1);
		// A concrete selection must not move as the picker filters, even when the
		// query excludes the selected entity from the filtered list.
		view.setQuery('bat');
		expect(view.filteredEntities.map((e) => e.id)).toEqual([0]);
		expect(view.selectedEntity?.id).toBe(1);
	});

	it('falls back to the unfiltered list head (not the search box) when entId is unresolved', () => {
		const view = seededView();
		view.mode = 'entity';
		// An entId that resolves to no concrete entity for the kind (e.g. initial
		// state or a kind-switch landing on a missing id).
		view.entId = 99;
		expect(view.selectedEntity?.id).toBe(0);
		// Narrowing the picker to a different entity must not drag the dossier along.
		view.setQuery('gob');
		expect(view.filteredEntities.map((e) => e.id)).toEqual([1]);
		expect(view.selectedEntity?.id).toBe(0);
	});
});

describe('StatisticsView data wiring', () => {
	it('starts empty and loading until statistics are fetched', () => {
		const view = new StatisticsView();
		expect(view.loading).toBe(true);
		expect(view.data.isEmpty).toBe(true);
	});

	it('builds the catalogue + entities from staticData once stats arrive', () => {
		const view = seededView();
		expect(view.data.statTypes).toHaveLength(15);
		expect(view.data.entityList('enemy').map((e) => e.name)).toEqual(['Cave Bat', 'Goblin']);
		// isBoss / order are resolved from the raw reference data.
		expect(view.data.entity('enemy', 1)?.boss).toBe(true);
		expect(view.data.entity('zone', 0)?.zoneNum).toBe(1);
		expect(view.data.statHeadline(EStatisticType.EnemiesKilled)).toBe(120);
	});
});
