/* Statistics screen — the player's tracked statistics, navigable two ways that
   cross-link into one system:
     · "By statistic" — the 14 EStatisticTypes grouped into Combat / Survival /
       Exploration / Time, each a card with its grand total and a per-entity
       breakdown (click an entity to pivot to it).
     · "By entity" — pick an enemy / zone / skill and see every statistic that
       references it, with this entity's rank among its peers (click a stat to
       jump back to its category).

   The statistic→entity-kind mapping mirrors how the backend actually *records*
   statistics (`PlayerProgress.RecordBattleCompleted`): the stats it writes a
   per-entity row for break down by that entity; the rest are tracked only as a
   single grand total ("none"). Display metadata the backend doesn't model
   (unit, aggregation, comparison direction, category) is defined here.

   NOTE on FastestVictory: it is recorded *per enemy* (and as a global min), so
   it is treated as an enemy statistic here — but `StatisticType.GetEntityType`
   on the backend currently returns `None` for it, an inconsistency flagged for
   the backend follow-up. The display data is sourced from a temporary mock (see
   statistics-mock.ts); wiring real backend data is the same follow-up. */

import { EStatisticType, type IPlayerStatistic } from '$lib/api';
import { normalizeText } from '$lib/common';
import { buildMockStatistics, MOCK_ENTITIES, type StatEntity } from './statistics-mock';

export type StatUnit = 'count' | 'damage' | 'time';
export type StatAgg = 'sum' | 'max' | 'min';
export type StatComp = 'AtLeast' | 'AtMost';
/** Entity a statistic can break down by. */
export type StatEntityKind = 'enemy' | 'zone' | 'skill';
/** A statistic either breaks down by an entity kind, or is a single total. */
export type StatKind = StatEntityKind | 'none';
export type StatCategory = 'combat' | 'survival' | 'exploration' | 'time';
export type ViewMode = 'stat' | 'entity';

export interface StatType {
	id: EStatisticType;
	name: string;
	kind: StatKind;
	unit: StatUnit;
	agg: StatAgg;
	comp: StatComp;
	cat: StatCategory;
	/** Only the killing of boss enemies is counted (display hint). */
	bossOnly?: boolean;
}

const def = (
	id: EStatisticType,
	kind: StatKind,
	unit: StatUnit,
	agg: StatAgg,
	comp: StatComp,
	cat: StatCategory,
	bossOnly = false
): StatType => ({ id, name: normalizeText(EStatisticType[id]), kind, unit, agg, comp, cat, bossOnly });

/** The statistic-type catalogue (mirrors EStatisticType + the backend's recorded
 *  entity breakdown). Ordered by category for the tab grouping. */
export const STAT_TYPES: StatType[] = [
	// Combat
	def(EStatisticType.EnemiesKilled, 'enemy', 'count', 'sum', 'AtLeast', 'combat'),
	def(EStatisticType.BossesDefeated, 'none', 'count', 'sum', 'AtLeast', 'combat', true),
	def(EStatisticType.SkillsUsed, 'skill', 'count', 'sum', 'AtLeast', 'combat'),
	def(EStatisticType.DamageDealt, 'skill', 'damage', 'sum', 'AtLeast', 'combat'),
	def(EStatisticType.HighestSingleAttackDamage, 'skill', 'damage', 'max', 'AtLeast', 'combat'),
	// Survival
	def(EStatisticType.DamageTaken, 'none', 'damage', 'sum', 'AtLeast', 'survival'),
	def(EStatisticType.DamageHealed, 'none', 'damage', 'sum', 'AtLeast', 'survival'),
	def(EStatisticType.PlayerDeaths, 'none', 'count', 'sum', 'AtMost', 'survival'),
	// Exploration
	def(EStatisticType.ZonesCleared, 'zone', 'count', 'sum', 'AtLeast', 'exploration'),
	def(EStatisticType.EnemiesEncountered, 'enemy', 'count', 'sum', 'AtLeast', 'exploration'),
	def(EStatisticType.BattlesWon, 'enemy', 'count', 'sum', 'AtLeast', 'exploration'),
	def(EStatisticType.BattlesLost, 'enemy', 'count', 'sum', 'AtMost', 'exploration'),
	// Time
	def(EStatisticType.TotalBattleTime, 'none', 'time', 'sum', 'AtLeast', 'time'),
	def(EStatisticType.FastestVictory, 'enemy', 'time', 'min', 'AtMost', 'time')
];

const STAT_BY_ID = new Map(STAT_TYPES.map((s) => [s.id, s]));

export const STAT_CATEGORIES: { key: StatCategory; label: string }[] = [
	{ key: 'combat', label: 'Combat' },
	{ key: 'survival', label: 'Survival' },
	{ key: 'exploration', label: 'Exploration' },
	{ key: 'time', label: 'Time' }
];

export const ENTITY_KINDS: StatEntityKind[] = ['enemy', 'zone', 'skill'];

/** One per-entity statistic row, resolved against its entity. */
export interface StatRow {
	entityId: number;
	value: number;
	entity: StatEntity;
}

/** A statistic that references a given entity, with that entity's standing. */
export interface EntityStatInfo {
	stat: StatType;
	value: number;
	rank: number;
	of: number;
	headline: number;
}

const aggregate = (values: number[], agg: StatAgg): number => {
	if (values.length === 0) {
		return 0;
	}
	if (agg === 'max') {
		return Math.max(...values);
	}
	if (agg === 'min') {
		return Math.min(...values);
	}
	return values.reduce((a, b) => a + b, 0);
};

/** Wraps a player's statistics + entity reference data with the queries the
 *  screen needs. Plain (non-reactive) so it is trivially unit-testable; the
 *  reactive {@link StatisticsView} holds an instance. */
export class StatisticsData {
	readonly stats: IPlayerStatistic[];
	readonly entities: Record<StatEntityKind, StatEntity[]>;

	constructor(stats: IPlayerStatistic[], entities: Record<StatEntityKind, StatEntity[]>) {
		this.stats = stats;
		this.entities = entities;
	}

	entityList(kind: StatEntityKind): StatEntity[] {
		return this.entities[kind] ?? [];
	}

	entity(kind: StatEntityKind, id: number): StatEntity | undefined {
		return this.entityList(kind).find((e) => e.id === id);
	}

	/** Per-entity rows for a statistic, resolved and sorted best-first (ascending
	 *  for "min" aggregates, where lower is better). Empty for `none` stats. */
	rowsForStat(type: EStatisticType): StatRow[] {
		const stat = STAT_BY_ID.get(type);
		if (!stat || stat.kind === 'none') {
			return [];
		}
		const kind = stat.kind;
		const rows: StatRow[] = [];
		for (const s of this.stats) {
			if (s.statisticTypeId !== type || s.entityId == null) {
				continue;
			}
			const entity = this.entity(kind, s.entityId);
			if (entity) {
				rows.push({ entityId: s.entityId, value: s.value, entity });
			}
		}
		rows.sort((a, b) => (stat.agg === 'min' ? a.value - b.value : b.value - a.value));
		return rows;
	}

	/** The grand-total headline for a statistic: the backend's null-entity row if
	 *  present, otherwise the aggregate of its per-entity rows. */
	statHeadline(type: EStatisticType): number {
		const totalRow = this.stats.find((s) => s.statisticTypeId === type && s.entityId == null);
		if (totalRow) {
			return totalRow.value;
		}
		const stat = STAT_BY_ID.get(type);
		return aggregate(
			this.rowsForStat(type).map((r) => r.value),
			stat?.agg ?? 'sum'
		);
	}

	statEntityCount(type: EStatisticType): number {
		return this.rowsForStat(type).length;
	}

	/** Every statistic that references the given entity, with the entity's value,
	 *  rank among peers, and the statistic's headline. */
	statsForEntity(kind: StatEntityKind, entityId: number): EntityStatInfo[] {
		const out: EntityStatInfo[] = [];
		for (const stat of STAT_TYPES) {
			if (stat.kind !== kind) {
				continue;
			}
			const rows = this.rowsForStat(stat.id);
			const idx = rows.findIndex((r) => r.entityId === entityId);
			if (idx < 0) {
				continue;
			}
			out.push({ stat, value: rows[idx].value, rank: idx + 1, of: rows.length, headline: this.statHeadline(stat.id) });
		}
		return out;
	}
}

/* ── reactive view-model ──────────────────────────────────────────────────── */

export class StatisticsView {
	readonly data: StatisticsData;

	/** Top-level view: by statistic or by entity. */
	mode = $state<ViewMode>('stat');
	/** Active category tab in the "by statistic" view (or `all`). */
	statCat = $state<StatCategory | 'all'>('combat');
	/** Active entity-kind tab in the "by entity" view. */
	entKind = $state<StatEntityKind>('enemy');
	/** Selected entity id in the "by entity" view. */
	entId = $state<number>(0);
	/** Entity-picker search query. */
	query = $state('');

	constructor(data?: StatisticsData) {
		this.data = data ?? new StatisticsData(buildMockStatistics(), MOCK_ENTITIES);
		this.entId = this.data.entityList('enemy')[0]?.id ?? 0;
	}

	/** Statistics shown in the current category tab. */
	readonly shownStats = $derived(
		this.statCat === 'all' ? STAT_TYPES : STAT_TYPES.filter((s) => s.cat === this.statCat)
	);

	/** Entities matching the picker query within the active kind. */
	readonly filteredEntities = $derived.by(() => {
		const q = this.query.trim().toLowerCase();
		const list = this.data.entityList(this.entKind);
		return q ? list.filter((e) => e.name.toLowerCase().includes(q)) : list;
	});

	/** The resolved selected entity (falls back to the first match / first item). */
	readonly selectedEntity = $derived.by(
		() =>
			this.data.entity(this.entKind, this.entId) ?? this.filteredEntities[0] ?? this.data.entityList(this.entKind)[0]
	);

	/** Every statistic referencing the selected entity. */
	readonly entityStats = $derived.by(() => {
		const entity = this.selectedEntity;
		return entity ? this.data.statsForEntity(this.entKind, entity.id) : [];
	});

	setMode(mode: ViewMode): void {
		this.mode = mode;
	}

	setStatCat(cat: StatCategory | 'all'): void {
		this.statCat = cat;
	}

	setEntId(id: number): void {
		this.entId = id;
	}

	setQuery(query: string): void {
		this.query = query;
	}

	/** Switch entity kind, resetting the selection + search to that kind. */
	switchKind(kind: StatEntityKind): void {
		this.entKind = kind;
		this.entId = this.data.entityList(kind)[0]?.id ?? 0;
		this.query = '';
	}

	/** Pivot from a stat card's entity row into that entity's dossier. */
	goEntity(kind: StatEntityKind, id: number): void {
		this.entKind = kind;
		this.entId = id;
		this.query = '';
		this.mode = 'entity';
	}

	/** Jump from an entity's stat tile back to that stat's category. */
	goStat(cat: StatCategory): void {
		this.statCat = cat;
		this.mode = 'stat';
	}
}
