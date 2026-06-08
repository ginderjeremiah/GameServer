import { describe, it, expect, beforeEach, vi } from 'vitest';
import { EEntityType, EStatisticType, type IPlayerStatistic } from '$lib/api';

// StatisticsView reads the statistic-type catalogue + entity lists from the
// in-memory staticData store, so it is mocked here.
const { staticData } = vi.hoisted(() => ({
	// eslint-disable-next-line @typescript-eslint/no-explicit-any
	staticData: {} as any
}));
vi.mock('$stores', () => ({ staticData }));

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

describe('StatisticsData.statsForEntity', () => {
	it('lists every statistic referencing an entity with its rank and headline', () => {
		const infos = data().statsForEntity('enemy', 0);
		const killed = infos.find((i) => i.stat.id === EStatisticType.EnemiesKilled)!;
		expect(killed).toMatchObject({ value: 100, rank: 1, of: 2, headline: 120 });
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

	it('filters entities by the search query', () => {
		const view = seededView();
		view.setQuery('gob');
		expect(view.filteredEntities.map((e) => e.id)).toEqual([1]);
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
