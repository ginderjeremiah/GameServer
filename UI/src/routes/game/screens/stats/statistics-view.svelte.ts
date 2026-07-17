/* Statistics screen — the player's tracked statistics, grouped "by statistic":
   the EStatisticTypes split into Combat / Survival / Exploration / Time, each a
   card with its grand total and a per-entity breakdown. Clicking an entity row
   deep-links into that entity's Codex dossier, where the per-entity statistics
   live now (the in-place "by entity" dossier was retired — see openEntity).

   The statistic→entity-kind mapping comes from the backend's statistic-type
   reference data (`IStatisticType.entityType`, loaded into
   `staticData.statisticTypes`), so the frontend no longer hard-codes which
   entity kind each statistic breaks down by. Display metadata the backend does
   not model (unit, aggregation, comparison direction, category, display order)
   is a presentation concern defined here in STAT_PRESENTATION and merged with
   the server metadata by buildStatTypes(). The actual values come from
   the `GetPlayerStatistics` socket command; entity ids resolve against the live staticData. */

import { EDamageTypeKey, EEntityType, EStatisticType, type IPlayerStatistic, type IStatisticType } from '$lib/api';
import { damageTypeKeyName } from '$lib/common';
import { navigation, staticData, statistics } from '$stores';
import { statCategoryLabel } from './statistics-display';
import type { CodexNavPayload } from '../codex/codex-view.svelte';

export type StatUnit = 'count' | 'damage' | 'time';
export type StatAgg = 'sum' | 'max' | 'min';
export type StatComp = 'AtLeast' | 'AtMost';
/** Dossier-navigable entity a statistic can break down by. */
export type StatEntityKind = 'enemy' | 'zone' | 'skill';
/** Any breakdown kind a statistic can have, dossier-navigable or not — `damageType` (#1473) has no
 *  dossier to pivot into, so it renders as a flat, non-interactive list instead of dossier-linked rows. */
export type StatBreakdownKind = StatEntityKind | 'damageType';
/** A statistic either breaks down by a kind, or is a single total. */
export type StatKind = StatBreakdownKind | 'none';
export type StatCategory = 'combat' | 'survival' | 'exploration' | 'time';

/** A reference entity a statistic can break down by — the shape `StatisticsData`
 *  resolves entity ids against (built from `staticData.enemies/zones/skills`). */
export interface StatEntity {
	id: number;
	name: string;
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

/** The presentation catalogue, ordered for the category-tab grouping. Covers every
 *  EStatisticType with a breakdown (dossier-navigable, or the flat damage-type list) or a
 *  global total; the entity breakdown (kind) + name come from the server. */
const STAT_PRESENTATION: StatPresentation[] = [
	// Combat
	{ id: EStatisticType.EnemiesKilled, unit: 'count', agg: 'sum', comp: 'AtLeast', cat: 'combat' },
	{ id: EStatisticType.BossesDefeated, unit: 'count', agg: 'sum', comp: 'AtLeast', cat: 'combat' },
	{ id: EStatisticType.SkillsUsed, unit: 'count', agg: 'sum', comp: 'AtLeast', cat: 'combat' },
	{ id: EStatisticType.DamageDealt, unit: 'damage', agg: 'sum', comp: 'AtLeast', cat: 'combat' },
	{ id: EStatisticType.HighestSingleAttackDamage, unit: 'damage', agg: 'max', comp: 'AtLeast', cat: 'combat' },
	{ id: EStatisticType.CriticalHits, unit: 'count', agg: 'sum', comp: 'AtLeast', cat: 'combat' },
	{ id: EStatisticType.CriticalDamageDealt, unit: 'damage', agg: 'sum', comp: 'AtLeast', cat: 'combat' },
	// The riposte damage (#1457) sits with the offense stats; the parry count/avoided damage are survival.
	{ id: EStatisticType.CounterDamageDealt, unit: 'damage', agg: 'sum', comp: 'AtLeast', cat: 'combat' },
	{ id: EStatisticType.KillsByDamageType, unit: 'count', agg: 'sum', comp: 'AtLeast', cat: 'combat' },
	// Survival
	{ id: EStatisticType.DamageTaken, unit: 'damage', agg: 'sum', comp: 'AtLeast', cat: 'survival' },
	{ id: EStatisticType.DamageHealed, unit: 'damage', agg: 'sum', comp: 'AtLeast', cat: 'survival' },
	{ id: EStatisticType.AttacksDodged, unit: 'count', agg: 'sum', comp: 'AtLeast', cat: 'survival' },
	{ id: EStatisticType.DamageDodged, unit: 'damage', agg: 'sum', comp: 'AtLeast', cat: 'survival' },
	{ id: EStatisticType.AttacksParried, unit: 'count', agg: 'sum', comp: 'AtLeast', cat: 'survival' },
	{ id: EStatisticType.DamageParried, unit: 'damage', agg: 'sum', comp: 'AtLeast', cat: 'survival' },
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

/** Maps the backend's entity type onto the screen's breakdown-kind discriminator. DamageType
 *  (#1455) has no Codex dossier to pivot into, so its rows render as a flat, non-interactive
 *  list (`StatBreakdownKind.damageType`) rather than dossier-linked rows (#1473). */
const KIND_BY_ENTITY_TYPE: Record<EEntityType, StatKind> = {
	[EEntityType.None]: 'none',
	[EEntityType.Enemy]: 'enemy',
	[EEntityType.Zone]: 'zone',
	[EEntityType.Skill]: 'skill',
	[EEntityType.DamageType]: 'damageType'
};

/** Every damage-type key, in enum order — the breakdown rows for `KillsByDamageType`. */
const DAMAGE_TYPE_KEYS = Object.values(EDamageTypeKey).filter((v): v is EDamageTypeKey => typeof v === 'number');

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

/** Builds the breakdown reference lists the screen resolves ids against: the dossier-navigable
 *  entities from the in-memory static reference data (enemies / zones / skills), plus the
 *  damage-type keys (a fixed enum, not reference data) using the shared player-facing labels. */
export function buildStatEntities(): Record<StatBreakdownKind, StatEntity[]> {
	return {
		enemy: (staticData.enemies ?? []).filter(Boolean).map((e) => ({ id: e.id, name: e.name })),
		zone: (staticData.zones ?? []).filter(Boolean).map((z) => ({ id: z.id, name: z.name })),
		skill: (staticData.skills ?? []).filter(Boolean).map((s) => ({ id: s.id, name: s.name })),
		damageType: DAMAGE_TYPE_KEYS.map((key) => ({ id: key, name: damageTypeKeyName(key) }))
	};
}

/** The four stat categories in display order; labels come from the single source in
 *  `statistics-display` so the tab labels and the category accents can't drift apart. */
const STAT_CATEGORY_KEYS: StatCategory[] = ['combat', 'survival', 'exploration', 'time'];
export const STAT_CATEGORIES: { key: StatCategory; label: string }[] = STAT_CATEGORY_KEYS.map((key) => ({
	key,
	label: statCategoryLabel(key)
}));

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
}

/** Memoised per-statistic display data, computed once on the immutable
 *  {@link StatisticsData} so the stat cards and dossier tiles read it as a prop
 *  rather than re-scanning every render. */
export interface StatSummary {
	/** Per-entity rows, resolved and sorted best-first (empty for `none` stats). */
	rows: StatRow[];
	/** Bar denominator — the leading row's value, floored at 1. */
	maxVal: number;
	/** Grand-total headline: the server's null-entity row, else the row aggregate. */
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
	readonly entities: Record<StatBreakdownKind, StatEntity[]>;
	/** Entity lookup per kind, built once so id resolution is O(1) (not Array.find per row). */
	private readonly entityById: Record<StatBreakdownKind, Map<number, StatEntity>>;
	/** Per-statistic display summaries, memoised once over the immutable inputs. */
	private readonly summaries: Map<EStatisticType, StatSummary>;

	constructor(statTypes: StatType[], stats: IPlayerStatistic[], entities: Record<StatBreakdownKind, StatEntity[]>) {
		this.statTypes = statTypes;
		this.stats = stats;
		this.entities = entities;
		this.entityById = {
			enemy: indexEntities(entities.enemy),
			zone: indexEntities(entities.zone),
			skill: indexEntities(entities.skill),
			damageType: indexEntities(entities.damageType)
		};
		// eslint-disable-next-line svelte/prefer-svelte-reactivity -- non-reactive memo map
		this.summaries = new Map(statTypes.map((s) => [s.id, this.computeSummary(s)]));
	}

	/** Statistic types in the given category, in catalogue (display) order. */
	statsInCategory(cat: StatCategory | 'all'): StatType[] {
		return cat === 'all' ? this.statTypes : this.statTypes.filter((s) => s.cat === cat);
	}

	entity(kind: StatBreakdownKind, id: number): StatEntity | undefined {
		return this.entityById[kind]?.get(id);
	}

	/** The memoised display summary for a statistic (rows + bar max + headline). */
	summaryFor(type: EStatisticType): StatSummary {
		return this.summaries.get(type) ?? EMPTY_SUMMARY;
	}

	/** Every statistic that references the given entity, with the entity's value
	 *  and rank among peers. */
	statsForEntity(kind: StatEntityKind, entityId: number): EntityStatInfo[] {
		const out: EntityStatInfo[] = [];
		for (const stat of this.statTypes) {
			if (stat.kind !== kind) {
				continue;
			}
			const rows = this.summaryFor(stat.id).rows;
			const idx = rows.findIndex((r) => r.entityId === entityId);
			if (idx < 0) {
				continue;
			}
			out.push({ stat, value: rows[idx].value, rank: idx + 1, of: rows.length });
		}
		return out;
	}

	/** Whether the player has recorded any statistic values at all (new player). */
	get isEmpty(): boolean {
		return this.stats.length === 0;
	}

	/** Computes a statistic's display summary once (called from the constructor). */
	private computeSummary(stat: StatType): StatSummary {
		const rows = this.computeRows(stat);
		const maxVal = Math.max(...rows.map((r) => r.value), 1);
		return { rows, maxVal, headline: this.computeHeadline(stat, rows) };
	}

	/** Resolves and sorts a statistic's per-entity rows (empty for `none` stats). */
	private computeRows(stat: StatType): StatRow[] {
		if (stat.kind === 'none') {
			return [];
		}
		const kind = stat.kind;
		const rows: StatRow[] = [];
		for (const s of this.stats) {
			if (s.statisticTypeId !== stat.id || s.entityId == null) {
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

	private computeHeadline(stat: StatType, rows: StatRow[]): number {
		const totalRow = this.stats.find((s) => s.statisticTypeId === stat.id && s.entityId == null);
		if (totalRow) {
			return totalRow.value;
		}
		return aggregate(
			rows.map((r) => r.value),
			stat.agg
		);
	}
}

/** Indexes an entity list by id for O(1) resolution. */
function indexEntities(list: StatEntity[]): Map<number, StatEntity> {
	return new Map((list ?? []).map((e) => [e.id, e]));
}

/** Shared empty summary for an unknown (off-catalogue) statistic id. */
const EMPTY_SUMMARY: StatSummary = { rows: [], maxVal: 1, headline: 0 };

/* ── reactive view-model ──────────────────────────────────────────────────── */

export class StatisticsView {
	/** The player's statistic values, read live from the shared store (fetched via the
	 *  `GetPlayerStatistics` socket command on mount) so a background update — e.g. the fight
	 *  screen's optimistic zone-clear — is reflected without remounting this screen. */
	readonly stats = $derived(statistics.stats);
	/** True until the statistic values have been fetched. */
	loading = $state(true);
	/** True when the fetch failed (distinct from a genuine empty result). */
	error = $state(false);

	/** Active category tab in the "by statistic" view (or `all`). */
	statCat = $state<StatCategory | 'all'>('combat');

	/** The query engine, rebuilt from the live reference data + fetched values. */
	readonly data = $derived(
		new StatisticsData(buildStatTypes(staticData.statisticTypes ?? []), this.stats, buildStatEntities())
	);

	/** Statistics shown in the current category tab. */
	readonly shownStats = $derived(this.data.statsInCategory(this.statCat));

	setStatCat(cat: StatCategory | 'all'): void {
		this.statCat = cat;
	}

	/** Open an entity from a stat-card row: deep-link into the matching Codex dossier, where the
	 *  per-entity statistics live now (the Statistics screen no longer renders an in-place dossier).
	 *  Enemies land on the dossier's Statistics sub-tab; zone/skill dossiers show their record inline. */
	openEntity(kind: StatEntityKind, id: number): void {
		const payload: CodexNavPayload =
			kind === 'enemy'
				? { tab: 'enemies', enemyId: id, sub: 'statistics' }
				: kind === 'zone'
					? { tab: 'zones', zoneId: id }
					: { tab: 'skills', skillId: id };
		navigation.requestScreen('codex', payload);
	}
}
