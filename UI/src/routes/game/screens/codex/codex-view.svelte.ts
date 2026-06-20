/* Codex screen — a read-only reference glossary of the game's enemies, zones and skills. Only the
   Enemies tab is built today; Zones and Skills show a "coming soon" placeholder (filed as follow-ups).

   The Enemies tab is a master/detail: a filterable enemy table beside a dossier with Attributes
   (live level-scaled stats + a "show scaling" breakdown), Statistics (the player's per-enemy record),
   Skills, Spawns and Challenges sub-tabs. The data is all live reference/runtime data — the screen
   reuses the real `BattleAttributes` enemy build for stat scaling (`enemy-stats`), the Statistics
   screen's per-entity query (`StatisticsData.statsForEntity`), and the challenge progress store —
   rather than hard-coding any of it. Per-entity statistics live here now; the Statistics screen
   deep-links an enemy into this dossier instead of rendering its own.

   The view-model only wires reactive state to the pure helpers; the projection maths live in
   `enemy-level` / `enemy-stats` (unit-tested directly). */

import { EEntityType, type IEnemy, type IPlayerStatistic } from '$lib/api';
import { challengeTypeColor, challengeTypeName } from '$lib/common';
import { playerChallenges, staticData } from '$stores';
import { fmtValue } from '../stats/statistics-display';
import { StatisticsData, buildStatEntities, buildStatTypes } from '../stats/statistics-view.svelte';
import {
	type CodexTab,
	type EnemyFilter,
	type EnemySubTab,
	CODEX_TABS,
	enemyAccent,
	enemyKindLabel,
	formatBand,
	formatCooldown,
	tabAccent,
	tabLabel
} from './codex-display';
import { type LevelRange, levelRange, spawnShare, zoneTotalWeight } from './enemy-level';
import { type EnemyAttributes, enemyAttributesAtLevel } from './enemy-stats';

/** A one-shot payload handed to the Codex via the navigation store (e.g. from the Statistics screen). */
export interface CodexNavPayload {
	tab?: CodexTab;
	enemyId?: number;
	sub?: EnemySubTab;
}

/* ── projected row/card view-models (presentational shapes the components render) ── */

export interface CodexTabVM {
	key: CodexTab;
	label: string;
	count: number;
	accent: string;
	active: boolean;
}

export interface EnemyRowVM {
	id: number;
	name: string;
	isBoss: boolean;
	band: string;
	zoneCount: number;
	skillCount: number;
	selected: boolean;
}

export interface SubTabVM {
	key: EnemySubTab;
	label: string;
}

export interface EnemySkillVM {
	id: number;
	name: string;
	meta: string;
}

export interface EnemySpawnVM {
	zoneId: number;
	zoneName: string;
	share: number;
	weightLabel: string;
}

export interface EnemyStatVM {
	label: string;
	value: string;
	rank: number;
	of: number;
}

export interface EnemyChallengeVM {
	id: number;
	name: string;
	typeLabel: string;
	accent: string;
	progressText: string;
	completed: boolean;
}

const SUB_TAB_DEFS: SubTabVM[] = [
	{ key: 'attributes', label: 'Attributes' },
	{ key: 'statistics', label: 'Statistics' },
	{ key: 'skills', label: 'Skills' },
	{ key: 'spawns', label: 'Spawns' }
];

/* ── reactive view-model ──────────────────────────────────────────────────── */

export class CodexView {
	/** Active top-level tab. */
	tab = $state<CodexTab>('enemies');
	/** Inspected enemy id (falls back to the head of the list when unresolved). */
	selectedEnemyId = $state<number>(-1);
	/** Active dossier sub-tab. */
	sub = $state<EnemySubTab>('attributes');
	/** Level the Attributes sub-tab scales the selected enemy to. */
	level = $state<number>(1);
	/** Whether the Attributes sub-tab reveals the base + per-level scaling breakdown. */
	scaling = $state<boolean>(false);
	/** Enemy-table filter chip. */
	filter = $state<EnemyFilter>('all');

	/** The player's statistic values (fetched on mount via the shared statistics store). */
	stats = $state<IPlayerStatistic[]>([]);
	statsLoading = $state(true);
	statsError = $state(false);

	constructor(payload?: CodexNavPayload) {
		if (payload?.tab) {
			this.tab = payload.tab;
		}
		if (payload?.sub) {
			this.sub = payload.sub;
		}
		// Resolve the initial enemy (the deep-link target, else the head of the list) so the table
		// highlights what the dossier shows. Reads stores directly to avoid touching a $derived here.
		const initial = this.resolveEnemy(payload?.enemyId ?? -1);
		if (initial) {
			this.selectedEnemyId = initial.id;
		}
		this.level = this.levelFor(initial);
	}

	/* ── catalogue ───────────────────────────────────────────────────────────── */

	/** Non-retired enemies — retirement keeps a slot resolvable but out of the glossary. */
	readonly enemies = $derived((staticData.enemies ?? []).filter((e) => !e.retiredAt));

	readonly filteredEnemies = $derived(
		this.enemies.filter((e) => this.filter === 'all' || (this.filter === 'boss' ? e.isBoss : !e.isBoss))
	);

	/** Top-level tabs with live counts + section accents. */
	readonly tabs = $derived.by<CodexTabVM[]>(() => {
		const counts: Record<CodexTab, number> = {
			enemies: this.enemies.length,
			zones: (staticData.zones ?? []).filter((z) => !z.retiredAt).length,
			skills: (staticData.skills ?? []).filter((s) => !s.retiredAt).length
		};
		return CODEX_TABS.map((key) => ({
			key,
			label: tabLabel(key),
			count: counts[key],
			accent: tabAccent(key),
			active: key === this.tab
		}));
	});

	/** Enemy table rows for the active filter. */
	readonly enemyRows = $derived.by<EnemyRowVM[]>(() => {
		const zones = staticData.zones ?? [];
		return this.filteredEnemies.map((e) => ({
			id: e.id,
			name: e.name,
			isBoss: e.isBoss,
			band: formatBand(levelRange(e, zones)),
			zoneCount: e.isBoss ? 1 : e.spawns.length,
			skillCount: e.skillPool.length,
			selected: e.id === this.selectedEnemyId
		}));
	});

	/** Number of enemies shown under the active filter (the "N shown" readout). */
	readonly shownCount = $derived(this.filteredEnemies.length);

	/* ── selected enemy + dossier ──────────────────────────────────────────────── */

	/** The inspected enemy, falling back to the head of the (filtered, then full) list. */
	readonly selectedEnemy = $derived.by<IEnemy | undefined>(
		() => this.enemies.find((e) => e.id === this.selectedEnemyId) ?? this.filteredEnemies[0] ?? this.enemies[0]
	);

	readonly range = $derived.by<LevelRange | undefined>(() => {
		const e = this.selectedEnemy;
		return e ? levelRange(e, staticData.zones ?? []) : undefined;
	});

	/** Header chrome for the dossier (kind label + section accent). */
	readonly dossierAccent = $derived(
		this.selectedEnemy ? enemyAccent(this.selectedEnemy.isBoss) : 'var(--enemy-accent)'
	);
	readonly dossierKind = $derived(this.selectedEnemy ? enemyKindLabel(this.selectedEnemy.isBoss) : '');

	/** Primary + derived-secondary attribute values for the selected enemy at the current level. */
	readonly attributes = $derived.by<EnemyAttributes>(() => {
		const e = this.selectedEnemy;
		return e ? enemyAttributesAtLevel(e, this.level) : { primary: [], secondary: [] };
	});

	/** Bar denominator for the primary stat bars (the leading value, floored at 1). */
	readonly maxPrimary = $derived(Math.max(1, ...this.attributes.primary.map((p) => p.value)));

	readonly skillRows = $derived.by<EnemySkillVM[]>(() => {
		const e = this.selectedEnemy;
		const skills = staticData.skills ?? [];
		if (!e) {
			return [];
		}
		return e.skillPool
			.map((id) => skills[id])
			.filter(Boolean)
			.map((sk) => ({
				id: sk.id,
				name: sk.name,
				meta: `${sk.baseDamage > 0 ? `base ${sk.baseDamage}` : 'utility'} · ${formatCooldown(sk.cooldownMs)} cd`
			}));
	});

	readonly spawnHeading = $derived.by(() => {
		const e = this.selectedEnemy;
		if (!e) {
			return '';
		}
		if (e.isBoss) {
			return 'Encounter';
		}
		const n = e.spawns.length;
		return `Spawns in ${n} ${n === 1 ? 'zone' : 'zones'}`;
	});

	readonly spawns = $derived.by<EnemySpawnVM[]>(() => {
		const e = this.selectedEnemy;
		const zones = staticData.zones ?? [];
		if (!e) {
			return [];
		}
		if (e.isBoss) {
			const zone = zones.find((z) => z?.bossEnemyId === e.id);
			return zone ? [{ zoneId: zone.id, zoneName: zone.name, share: 100, weightLabel: 'boss fight' }] : [];
		}
		return e.spawns
			.map((sp) => ({
				zoneId: sp.zoneId,
				zoneName: zones[sp.zoneId]?.name ?? `Zone ${sp.zoneId}`,
				share: spawnShare(sp.weight, zoneTotalWeight(sp.zoneId, this.enemies)),
				weightLabel: `weight ${sp.weight}`
			}))
			.sort((a, b) => b.share - a.share);
	});

	/** The statistic query engine, rebuilt from the live reference data + fetched values. */
	readonly statData = $derived(
		new StatisticsData(buildStatTypes(staticData.statisticTypes ?? []), this.stats, buildStatEntities())
	);

	/** Every statistic that references the selected enemy (the dossier's "your record"). */
	readonly statistics = $derived.by<EnemyStatVM[]>(() => {
		const e = this.selectedEnemy;
		if (!e) {
			return [];
		}
		return this.statData.statsForEntity('enemy', e.id).map((info) => ({
			label: info.stat.name,
			value: fmtValue(info.value, info.stat.unit),
			rank: info.rank,
			of: info.of
		}));
	});

	/** Challenges scoped to the selected enemy, with progress from the shared challenge store. */
	readonly challenges = $derived.by<EnemyChallengeVM[]>(() => {
		const e = this.selectedEnemy;
		if (!e) {
			return [];
		}
		// eslint-disable-next-line svelte/prefer-svelte-reactivity -- transient lookup, not held state
		const progressById = new Map(playerChallenges.all.map((pc) => [pc.challengeId, pc]));
		return (staticData.challenges ?? [])
			.filter((c) => c.entityType === EEntityType.Enemy && c.targetEntityId === e.id)
			.map((c) => {
				const pc = progressById.get(c.id);
				const progress = pc?.progress ?? 0;
				const completed = pc?.completed ?? false;
				const progressText =
					c.progressGoal === 1 ? (completed ? 'done' : 'sealed') : `${Math.round(progress)}/${c.progressGoal}`;
				return {
					id: c.id,
					name: c.name,
					typeLabel: challengeTypeName(c.challengeTypeId, staticData.challengeTypes),
					accent: challengeTypeColor(c.challengeTypeId),
					progressText,
					completed
				};
			});
	});

	/** Dossier sub-tabs — Challenges only when the enemy has related challenges. */
	readonly subTabs = $derived.by<SubTabVM[]>(() =>
		this.challenges.length > 0 ? [...SUB_TAB_DEFS, { key: 'challenges', label: 'Challenges' }] : SUB_TAB_DEFS
	);

	/* ── handlers ──────────────────────────────────────────────────────────────── */

	selectTab(tab: CodexTab): void {
		this.tab = tab;
	}

	/** Select an enemy: reset to the Attributes sub-tab and reseed the level to its band. */
	selectEnemy(id: number): void {
		this.selectedEnemyId = id;
		this.sub = 'attributes';
		this.level = this.levelFor(this.resolveEnemy(id));
	}

	selectSub(sub: EnemySubTab): void {
		this.sub = sub;
	}

	setLevel(level: number): void {
		this.level = level;
	}

	toggleScaling(): void {
		this.scaling = !this.scaling;
	}

	setFilter(filter: EnemyFilter): void {
		this.filter = filter;
	}

	/* ── helpers (store reads, kept off the reactive graph for the constructor) ──── */

	/** Resolve an enemy id against the catalogue, falling back to the head of the list. */
	private resolveEnemy(id: number): IEnemy | undefined {
		const enemies = (staticData.enemies ?? []).filter((e) => !e.retiredAt);
		return enemies.find((e) => e.id === id) ?? enemies[0];
	}

	/** The level to scale an enemy to by default: the boss's fixed level, else its band midpoint. */
	private levelFor(enemy: IEnemy | undefined): number {
		if (!enemy) {
			return 1;
		}
		const r = levelRange(enemy, staticData.zones ?? []);
		return r.fixed ? r.min : Math.round((r.min + r.max) / 2);
	}
}
