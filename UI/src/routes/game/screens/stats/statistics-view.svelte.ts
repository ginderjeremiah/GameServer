/* Statistics screen — the player's tracked statistics, navigable two ways that
   cross-link into one system:
     · "By statistic" — the 14 EStatisticTypes grouped into Combat / Survival /
       Exploration / Time, each a card with its grand total and a per-entity
       breakdown (click an entity to pivot to it).
     · "By entity" — pick an enemy / zone / skill and see every statistic that
       references it, with this entity's rank among its peers (click a stat to
       jump back to its category).

   The statistic→entity-kind mapping comes from the backend's statistic-type
   reference data (`IStatisticType.entityType`, loaded into
   `staticData.statisticTypes`), so the frontend no longer hard-codes which
   entity kind each statistic breaks down by. Display metadata the backend does
   not model (unit, aggregation, comparison direction, category, display order)
   is a presentation concern defined here in STAT_PRESENTATION and merged with
   the server metadata by buildStatTypes(). The actual values come from
   the `GetPlayerStatistics` socket command; entity ids resolve against the live staticData. */

import { EEntityType, EStatisticType, type IPlayerStatistic, type IStatisticType } from '$lib/api';
import { staticData } from '$stores';
import { statCategoryLabel } from './statistics-display';

export type StatUnit = 'count' | 'damage' | 'time';
export type StatAgg = 'sum' | 'max' | 'min';
export type StatComp = 'AtLeast' | 'AtMost';
/** Entity a statistic can break down by. */
export type StatEntityKind = 'enemy' | 'zone' | 'skill';
/** A statistic either breaks down by an entity kind, or is a single total. */
export type StatKind = StatEntityKind | 'none';
export type StatCategory = 'combat' | 'survival' | 'exploration' | 'time';
export type ViewMode = 'stat' | 'entity';

/** A reference entity a statistic can break down by — the shape `StatisticsData`
 *  resolves entity ids against (built from `staticData.enemies/zones/skills`). */
export interface StatEntity {
	id: number;
	name: string;
	/** Boss flag (enemies only). */
	boss?: boolean;
	/** Zone order number (zones only). */
	zoneNum?: number;
}

export interface StatType {
	id: EStatisticType;
	name: string;
	kind: StatKind;
	unit: StatUnit;
	agg: StatAgg;
	comp: StatComp;
	cat: StatCategory;
	/** Only the killing of boss enemies is counted — sourced from the server's
	 *  statistic-type metadata (`IStatisticType.bossOnly`), the single source of
	 *  truth shared with the admin challenge editor. */
	bossOnly: boolean;
}

/** Presentation metadata for a statistic type — the concerns the backend does
 *  not model (unit, aggregation, comparison, category) plus the display order.
 *  Merged with the server's `entityType`, `bossOnly` + `name` by {@link buildStatTypes}. */
interface StatPresentation {
	id: EStatisticType;
	unit: StatUnit;
	agg: StatAgg;
	comp: StatComp;
	cat: StatCategory;
}

/** The presentation catalogue, ordered for the category-tab grouping. Covers
 *  every EStatisticType; the entity breakdown (kind) + name come from the server. */
const STAT_PRESENTATION: StatPresentation[] = [
	// Combat
	{ id: EStatisticType.EnemiesKilled, unit: 'count', agg: 'sum', comp: 'AtLeast', cat: 'combat' },
	{ id: EStatisticType.BossesDefeated, unit: 'count', agg: 'sum', comp: 'AtLeast', cat: 'combat' },
	{ id: EStatisticType.SkillsUsed, unit: 'count', agg: 'sum', comp: 'AtLeast', cat: 'combat' },
	{ id: EStatisticType.DamageDealt, unit: 'damage', agg: 'sum', comp: 'AtLeast', cat: 'combat' },
	{ id: EStatisticType.HighestSingleAttackDamage, unit: 'damage', agg: 'max', comp: 'AtLeast', cat: 'combat' },
	// Survival
	{ id: EStatisticType.DamageTaken, unit: 'damage', agg: 'sum', comp: 'AtLeast', cat: 'survival' },
	{ id: EStatisticType.DamageHealed, unit: 'damage', agg: 'sum', comp: 'AtLeast', cat: 'survival' },
	{ id: EStatisticType.PlayerDeaths, unit: 'count', agg: 'sum', comp: 'AtMost', cat: 'survival' },
	// Exploration
	{ id: EStatisticType.ZonesCleared, unit: 'count', agg: 'sum', comp: 'AtLeast', cat: 'exploration' },
	{ id: EStatisticType.EnemiesEncountered, unit: 'count', agg: 'sum', comp: 'AtLeast', cat: 'exploration' },
	{ id: EStatisticType.BattlesWon, unit: 'count', agg: 'sum', comp: 'AtLeast', cat: 'exploration' },
	{ id: EStatisticType.BattlesLost, unit: 'count', agg: 'sum', comp: 'AtMost', cat: 'exploration' },
	{ id: EStatisticType.BattlesAbandoned, unit: 'count', agg: 'sum', comp: 'AtMost', cat: 'exploration' },
	// Time
	{ id: EStatisticType.TotalBattleTime, unit: 'time', agg: 'sum', comp: 'AtLeast', cat: 'time' },
	{ id: EStatisticType.FastestVictory, unit: 'time', agg: 'min', comp: 'AtMost', cat: 'time' }
];

/** Maps the backend's entity type onto the screen's entity-kind discriminator. */
const KIND_BY_ENTITY_TYPE: Record<EEntityType, StatKind> = {
	[EEntityType.None]: 'none',
	[EEntityType.Enemy]: 'enemy',
	[EEntityType.Zone]: 'zone',
	[EEntityType.Skill]: 'skill'
};

/** Builds the statistic-type catalogue by merging the frontend presentation
 *  metadata with the backend statistic-type reference data (name + entity
 *  breakdown). Statistics absent from the server metadata are skipped. */
export function buildStatTypes(statisticTypes: IStatisticType[]): StatType[] {
	// eslint-disable-next-line svelte/prefer-svelte-reactivity -- non-reactive lookup map
	const metaById = new Map(statisticTypes.map((s) => [s.id, s]));
	const out: StatType[] = [];
	for (const p of STAT_PRESENTATION) {
		const meta = metaById.get(p.id);
		if (!meta) {
			continue;
		}
		out.push({
			id: p.id,
			name: meta.name,
			kind: KIND_BY_ENTITY_TYPE[meta.entityType] ?? 'none',
			unit: p.unit,
			agg: p.agg,
			comp: p.comp,
			cat: p.cat,
			bossOnly: meta.bossOnly
		});
	}
	return out;
}

/** Builds the entity reference lists the screen resolves ids against from the
 *  in-memory static reference data (enemies / zones / skills). */
export function buildStatEntities(): Record<StatEntityKind, StatEntity[]> {
	return {
		enemy: (staticData.enemies ?? []).filter(Boolean).map((e) => ({ id: e.id, name: e.name, boss: e.isBoss })),
		zone: (staticData.zones ?? []).filter(Boolean).map((z) => ({ id: z.id, name: z.name, zoneNum: z.order })),
		skill: (staticData.skills ?? []).filter(Boolean).map((s) => ({ id: s.id, name: s.name }))
	};
}

/** The four stat categories in display order; labels come from the single source in
 *  `statistics-display` so the tab labels and the category accents can't drift apart. */
const STAT_CATEGORY_KEYS: StatCategory[] = ['combat', 'survival', 'exploration', 'time'];
export const STAT_CATEGORIES: { key: StatCategory; label: string }[] = STAT_CATEGORY_KEYS.map((key) => ({
	key,
	label: statCategoryLabel(key)
}));

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

/** Wraps a player's statistics + the statistic-type catalogue + entity reference
 *  data with the queries the screen needs. Plain (non-reactive) so it is
 *  trivially unit-testable; the reactive {@link StatisticsView} holds an instance. */
export class StatisticsData {
	readonly statTypes: StatType[];
	readonly stats: IPlayerStatistic[];
	readonly entities: Record<StatEntityKind, StatEntity[]>;
	private readonly byId: Map<EStatisticType, StatType>;

	constructor(statTypes: StatType[], stats: IPlayerStatistic[], entities: Record<StatEntityKind, StatEntity[]>) {
		this.statTypes = statTypes;
		this.stats = stats;
		this.entities = entities;
		// eslint-disable-next-line svelte/prefer-svelte-reactivity -- non-reactive lookup map
		this.byId = new Map(statTypes.map((s) => [s.id, s]));
	}

	statType(type: EStatisticType): StatType | undefined {
		return this.byId.get(type);
	}

	/** Statistic types in the given category, in catalogue (display) order. */
	statsInCategory(cat: StatCategory | 'all'): StatType[] {
		return cat === 'all' ? this.statTypes : this.statTypes.filter((s) => s.cat === cat);
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
		const stat = this.byId.get(type);
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
		const stat = this.byId.get(type);
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
		for (const stat of this.statTypes) {
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

	/** Whether the player has recorded any statistic values at all (new player). */
	get isEmpty(): boolean {
		return this.stats.length === 0;
	}
}

/* ── reactive view-model ──────────────────────────────────────────────────── */

export class StatisticsView {
	/** The player's statistic values, fetched via the `GetPlayerStatistics` socket command on mount. */
	stats = $state<IPlayerStatistic[]>([]);
	/** True until the statistic values have been fetched. */
	loading = $state(true);
	/** True when the fetch failed (distinct from a genuine empty result). */
	error = $state(false);

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

	/** The query engine, rebuilt from the live reference data + fetched values. */
	readonly data = $derived(
		new StatisticsData(buildStatTypes(staticData.statisticTypes ?? []), this.stats, buildStatEntities())
	);

	/** Statistics shown in the current category tab. */
	readonly shownStats = $derived(this.data.statsInCategory(this.statCat));

	/** Entities matching the picker query within the active kind. */
	readonly filteredEntities = $derived.by(() => {
		const q = this.query.trim().toLowerCase();
		const list = this.data.entityList(this.entKind);
		return q ? list.filter((e) => e.name.toLowerCase().includes(q)) : list;
	});

	/** The resolved selected entity. When `entId` doesn't resolve to a concrete
	 *  entity it falls back to the head of the *unfiltered* list — never the
	 *  search-filtered list — so typing in the picker narrows the list without
	 *  moving the active dossier. */
	readonly selectedEntity = $derived.by(
		() => this.data.entity(this.entKind, this.entId) ?? this.data.entityList(this.entKind)[0]
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
