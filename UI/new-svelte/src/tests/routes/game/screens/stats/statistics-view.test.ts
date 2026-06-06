import { describe, it, expect } from 'vitest';
import { EStatisticType, type IPlayerStatistic } from '$lib/api';
import {
	StatisticsData,
	StatisticsView,
	STAT_TYPES,
	type StatEntity
} from '$routes/game/screens/stats/statistics-view.svelte';

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

const data = () => new StatisticsData(stats, entities);

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
		const d = new StatisticsData([{ statisticTypeId: EStatisticType.EnemiesKilled, entityId: 99, value: 5 }], entities);
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

describe('StatisticsView navigation', () => {
	it('initialises to the by-statistic view on the first enemy', () => {
		const view = new StatisticsView(data());
		expect(view.mode).toBe('stat');
		expect(view.statCat).toBe('combat');
		expect(view.entKind).toBe('enemy');
		expect(view.entId).toBe(0);
	});

	it('filters the shown statistics by the active category', () => {
		const view = new StatisticsView(data());
		expect(view.shownStats.every((s) => s.cat === 'combat')).toBe(true);
		view.setStatCat('time');
		expect(view.shownStats.map((s) => s.id)).toContain(EStatisticType.FastestVictory);
	});

	it('switchKind resets the selection and query to the new kind', () => {
		const view = new StatisticsView(data());
		view.setQuery('bat');
		view.switchKind('skill');
		expect(view.entKind).toBe('skill');
		expect(view.entId).toBe(0);
		expect(view.query).toBe('');
	});

	it('goEntity pivots into an entity dossier', () => {
		const view = new StatisticsView(data());
		view.goEntity('enemy', 1);
		expect(view.mode).toBe('entity');
		expect(view.selectedEntity?.id).toBe(1);
		expect(view.entityStats.some((i) => i.stat.id === EStatisticType.EnemiesKilled)).toBe(true);
	});

	it('goStat jumps back to a statistic category', () => {
		const view = new StatisticsView(data());
		view.goEntity('enemy', 0);
		view.goStat('exploration');
		expect(view.mode).toBe('stat');
		expect(view.statCat).toBe('exploration');
	});

	it('filters entities by the search query', () => {
		const view = new StatisticsView(data());
		view.setQuery('gob');
		expect(view.filteredEntities.map((e) => e.id)).toEqual([1]);
	});
});

describe('STAT_TYPES catalogue', () => {
	it('covers all 14 EStatisticType values', () => {
		expect(STAT_TYPES).toHaveLength(14);
	});

	it('keys FastestVictory to enemies and treats it as lower-is-better', () => {
		const fastest = STAT_TYPES.find((s) => s.id === EStatisticType.FastestVictory)!;
		expect(fastest.kind).toBe('enemy');
		expect(fastest.comp).toBe('AtMost');
		expect(fastest.agg).toBe('min');
	});

	it('treats deaths / damage-taken / total-time as total-only (none)', () => {
		for (const id of [EStatisticType.PlayerDeaths, EStatisticType.DamageTaken, EStatisticType.TotalBattleTime]) {
			expect(STAT_TYPES.find((s) => s.id === id)!.kind).toBe('none');
		}
	});
});

describe('default mock data source', () => {
	it('builds a usable view without injected data', () => {
		const view = new StatisticsView();
		expect(view.data.entityList('enemy').length).toBeGreaterThan(0);
		expect(view.data.statHeadline(EStatisticType.EnemiesKilled)).toBeGreaterThan(0);
	});
});
