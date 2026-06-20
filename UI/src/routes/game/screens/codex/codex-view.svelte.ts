/* Codex screen — a read-only reference glossary of the game's enemies, zones and skills. The Enemies
   and Zones tabs are built; Skills shows a "coming soon" placeholder (filed as a follow-up).

   The Enemies tab is a master/detail: a filterable enemy table beside a dossier with Attributes
   (live level-scaled stats + a "show scaling" breakdown), Statistics (the player's per-enemy record),
   Skills, Spawns and Challenges sub-tabs. The Zones tab is a progression rail (a status dot per zone:
   cleared / unlocked / locked) beside a zone dossier — level band, spawn pool, boss card, spawn table
   and unlock condition — whose boss card and spawn rows cross-link into the enemy dossier. The data is
   all live reference/runtime data — the screen reuses the real `BattleAttributes` enemy build for stat
   scaling (`$lib/common/enemy-attributes`), the Statistics screen's per-entity query
   (`StatisticsData.statsForEntity`), per-zone clears (`statistics.isZoneCleared`) and the challenge
   progress store — rather than hard-coding any of it. Per-entity statistics live here now; the
   Statistics screen deep-links an enemy into this dossier instead of rendering its own.

   The view-model only wires reactive state to the pure helpers; the projection maths live in
   `enemy-level` and the shared `$lib/common/enemy-attributes` (unit-tested directly). */

import { EEntityType, type IEnemy, type IPlayerStatistic, type IZone } from '$lib/api';
import { type EnemyAttributes, challengeTypeColor, challengeTypeName, enemyAttributesAtLevel } from '$lib/common';
import { playerChallenges, staticData, statistics } from '$stores';
import { fmtValue } from '../stats/statistics-display';
import { StatisticsData, buildStatEntities, buildStatTypes } from '../stats/statistics-view.svelte';
import {
	type CodexTab,
	type EnemyFilter,
	type EnemySort,
	type EnemySubTab,
	type ZoneStatus,
	CODEX_TABS,
	enemyAccent,
	enemyKindLabel,
	formatBand,
	formatCooldown,
	matchesEnemySearch,
	resolveZoneStatus,
	sortEnemyRows,
	tabAccent,
	tabLabel
} from './codex-display';
import { type LevelRange, levelRange, spawnShare, zoneTotalWeight } from './enemy-level';

/** A one-shot payload handed to the Codex via the navigation store (e.g. from the Statistics screen). */
export interface CodexNavPayload {
	tab?: CodexTab;
	enemyId?: number;
	zoneId?: number;
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
	/** Numeric sort key for the level metric (the band's low end). */
	level: number;
	zoneCount: number;
	skillCount: number;
	/** Pre-lowercased search haystack: name + kind + zone names (spawn zones, or a boss's encounter zone). */
	searchText: string;
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

export interface ZoneRowVM {
	id: number;
	name: string;
	band: string;
	status: ZoneStatus;
	/** Enemies (excluding the boss) that spawn in this zone. */
	spawnCount: number;
	hasBoss: boolean;
	selected: boolean;
}

export interface ZoneBossVM {
	enemyId: number;
	name: string;
	level: number;
}

export interface ZoneSpawnVM {
	enemyId: number;
	enemyName: string;
	share: number;
	weightLabel: string;
}

export interface ZoneUnlockVM {
	challengeId: number;
	challengeName: string;
	/** Whether the gate is still sealed (the gating challenge isn't complete). */
	sealed: boolean;
}

const SUB_TAB_DEFS: SubTabVM[] = [
	{ key: 'attributes', label: 'Attributes' },
	{ key: 'statistics', label: 'Statistics' },
	{ key: 'skills', label: 'Skills' },
	{ key: 'spawns', label: 'Spawns' }
];

/* ── catalogue helpers (one definition, reused by the reactive deriveds and the constructor's
   non-reactive store reads) — retirement keeps a slot resolvable but out of the glossary. ── */

/** Live (non-retired) enemies. */
const liveEnemies = (enemies: IEnemy[] | undefined): IEnemy[] => (enemies ?? []).filter((e) => !e.retiredAt);

/** Live (non-retired) zones in authored progression order. */
const liveZones = (zones: IZone[] | undefined): IZone[] =>
	(zones ?? []).filter((z) => !z.retiredAt).sort((a, b) => a.order - b.order);

/* ── reactive view-model ──────────────────────────────────────────────────── */

export class CodexView {
	/** Active top-level tab. */
	tab = $state<CodexTab>('enemies');
	/** Inspected enemy id (falls back to the head of the list when unresolved). */
	selectedEnemyId = $state<number>(-1);
	/** Inspected zone id (falls back to the head of the rail when unresolved). */
	selectedZoneId = $state<number>(-1);
	/** Active dossier sub-tab. */
	sub = $state<EnemySubTab>('attributes');
	/** Level the Attributes sub-tab scales the selected enemy to. */
	level = $state<number>(1);
	/** Whether the Attributes sub-tab reveals the base + per-level scaling breakdown. */
	scaling = $state<boolean>(false);
	/** Enemy-table filter chip. */
	filter = $state<EnemyFilter>('all');
	/** Enemy-table search query (matched against name, kind and spawn zones). */
	search = $state('');
	/** Enemy-table sort metric. */
	sort = $state<EnemySort>('level');

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
		// Same for the zone rail, so its selection is highlighted from the first render.
		const initialZone = this.resolveZone(payload?.zoneId ?? -1);
		if (initialZone) {
			this.selectedZoneId = initialZone.id;
		}
	}

	/* ── catalogue ───────────────────────────────────────────────────────────── */

	readonly enemies = $derived(liveEnemies(staticData.enemies));

	readonly filteredEnemies = $derived(
		this.enemies.filter((e) => this.filter === 'all' || (this.filter === 'boss' ? e.isBoss : !e.isBoss))
	);

	/** Top-level tabs with live counts + section accents. */
	readonly tabs = $derived.by<CodexTabVM[]>(() => {
		const counts: Record<CodexTab, number> = {
			enemies: this.enemies.length,
			zones: this.zones.length,
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

	/** Enemy table rows for the active filter, narrowed by the search query and ordered by the
	 *  active sort. The search/sort maths are the pure helpers in `codex-display`. */
	readonly enemyRows = $derived.by<EnemyRowVM[]>(() => {
		const zones = staticData.zones ?? [];
		return this.filteredEnemies
			.map((e) => {
				const range = levelRange(e, zones);
				// Bosses don't populate `spawns`; resolve their encounter zone the same way the dossier
				// does so a boss is findable by that zone name. Normal enemies use their spawn zones.
				const zoneNames = e.isBoss
					? [zones.find((z) => z?.bossEnemyId === e.id)?.name ?? '']
					: e.spawns.map((sp) => zones[sp.zoneId]?.name ?? '');
				return {
					id: e.id,
					name: e.name,
					isBoss: e.isBoss,
					band: formatBand(range),
					level: range.min,
					zoneCount: e.isBoss ? 1 : e.spawns.length,
					skillCount: e.skillPool.length,
					searchText: [e.name, enemyKindLabel(e.isBoss), ...zoneNames].join(' ').toLowerCase(),
					selected: e.id === this.selectedEnemyId
				};
			})
			.filter((row) => matchesEnemySearch(row, this.search))
			.sort(sortEnemyRows(this.sort));
	});

	/** Number of enemies shown under the active filter + search (the "N shown" readout). */
	readonly shownCount = $derived(this.enemyRows.length);

	/* ── selected enemy + dossier ──────────────────────────────────────────────── */

	/** The inspected enemy, falling back to the head of the visible rows, then the full list. */
	readonly selectedEnemy = $derived.by<IEnemy | undefined>(() => {
		const explicit = this.enemies.find((e) => e.id === this.selectedEnemyId);
		if (explicit) {
			return explicit;
		}
		const firstVisible = this.enemyRows[0];
		return (firstVisible && this.enemies.find((e) => e.id === firstVisible.id)) ?? this.enemies[0];
	});

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

	/* ── zones catalogue (the progression rail) ─────────────────────────────────── */

	readonly zones = $derived(liveZones(staticData.zones));

	/** Zone rail rows: a status dot (cleared / unlocked / locked), the level band, the spawn-pool size
	 *  and whether the zone has a dedicated boss. */
	readonly zoneRows = $derived.by<ZoneRowVM[]>(() => {
		const enemies = this.enemies;
		return this.zones.map((z) => ({
			id: z.id,
			name: z.name,
			band: formatBand({ min: z.levelMin, max: z.levelMax, fixed: false }),
			status: resolveZoneStatus(statistics.isZoneCleared(z.id), this.isZoneLocked(z)),
			spawnCount: enemies.filter((e) => e.spawns.some((s) => s.zoneId === z.id)).length,
			hasBoss: z.bossEnemyId != null,
			selected: z.id === this.selectedZoneId
		}));
	});

	/* ── selected zone + dossier ────────────────────────────────────────────────── */

	/** The inspected zone, falling back to the head of the rail. */
	readonly selectedZone = $derived<IZone | undefined>(
		this.zones.find((z) => z.id === this.selectedZoneId) ?? this.zones[0]
	);

	/** The selected zone's level band (`11–22`). */
	readonly zoneBand = $derived.by<string>(() => {
		const z = this.selectedZone;
		return z ? formatBand({ min: z.levelMin, max: z.levelMax, fixed: false }) : '';
	});

	/** The selected zone's progression status (drives the dossier seal + header accent). */
	readonly selectedZoneStatus = $derived.by<ZoneStatus>(() => {
		const z = this.selectedZone;
		return z ? resolveZoneStatus(statistics.isZoneCleared(z.id), this.isZoneLocked(z)) : 'unlocked';
	});

	/** The zone's dedicated boss (its card cross-links to the enemy dossier), or null when none is authored. */
	readonly zoneBoss = $derived.by<ZoneBossVM | null>(() => {
		const z = this.selectedZone;
		if (!z || z.bossEnemyId == null) {
			return null;
		}
		const boss = (staticData.enemies ?? [])[z.bossEnemyId];
		return boss ? { enemyId: boss.id, name: boss.name, level: z.bossLevel } : null;
	});

	/** The zone's spawn table — every non-retired enemy that spawns here, with its share of the zone's
	 *  total spawn weight, ordered by share. Bosses don't populate `spawns`, so they're naturally absent. */
	readonly zoneSpawns = $derived.by<ZoneSpawnVM[]>(() => {
		const z = this.selectedZone;
		if (!z) {
			return [];
		}
		const total = zoneTotalWeight(z.id, this.enemies);
		return this.enemies
			.flatMap((e) => {
				const spawn = e.spawns.find((s) => s.zoneId === z.id);
				return spawn ? [{ enemy: e, weight: spawn.weight }] : [];
			})
			.map(({ enemy, weight }) => ({
				enemyId: enemy.id,
				enemyName: enemy.name,
				share: spawnShare(weight, total),
				weightLabel: `weight ${weight}`
			}))
			.sort((a, b) => b.share - a.share);
	});

	/** Number of distinct enemies in the zone's spawn pool (the dossier's "N spawns" readout). */
	readonly zoneSpawnCount = $derived(this.zoneSpawns.length);

	/** The zone's unlock condition — the gating challenge's name plus whether it's still sealed — or
	 *  null for an always-open zone. */
	readonly zoneUnlock = $derived.by<ZoneUnlockVM | null>(() => {
		const z = this.selectedZone;
		if (!z || z.unlockChallengeId == null) {
			return null;
		}
		const challenge = (staticData.challenges ?? [])[z.unlockChallengeId];
		return {
			challengeId: z.unlockChallengeId,
			challengeName: challenge?.name ?? `Challenge ${z.unlockChallengeId}`,
			sealed: !playerChallenges.isChallengeCompleted(z.unlockChallengeId)
		};
	});

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

	selectZone(id: number): void {
		this.selectedZoneId = id;
	}

	/** Cross-link: jump to an enemy's dossier from the Zones tab (boss card / spawn row). */
	openEnemy(id: number): void {
		this.tab = 'enemies';
		this.selectEnemy(id);
	}

	/* ── helpers (store reads, kept off the reactive graph for the constructor) ──── */

	/** Resolve an enemy id against the catalogue, falling back to the head of the list. */
	private resolveEnemy(id: number): IEnemy | undefined {
		const enemies = liveEnemies(staticData.enemies);
		return enemies.find((e) => e.id === id) ?? enemies[0];
	}

	/** Resolve a zone id against the rail (authored order), falling back to the head. */
	private resolveZone(id: number): IZone | undefined {
		const zones = liveZones(staticData.zones);
		return zones.find((z) => z.id === id) ?? zones[0];
	}

	/** A zone is locked when it carries an unlock gate the player hasn't completed yet. */
	private isZoneLocked(zone: IZone): boolean {
		return zone.unlockChallengeId != null && !playerChallenges.isChallengeCompleted(zone.unlockChallengeId);
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
