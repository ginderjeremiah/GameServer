/* The Enemies tab's reactive layer: table filter/search/sort + the selected enemy's dossier
   (attributes, skills, spawns, challenges, statistics). Split out of CodexView so each tab's
   derivations are independently readable/testable — see codex-view.svelte.ts for the composing
   orchestrator and the cross-tab concerns (active tab, stats fetch, cross-links). */

import { EEntityType, type IEnemy } from '$lib/api';
import {
	type EnemyAttributes,
	challengeTypeColor,
	challengeTypeName,
	comparisonFor,
	enemyAttributesAtLevel,
	formatTime,
	progressInfo
} from '$lib/common';
import { playerChallenges, staticData } from '$stores';
import {
	type EnemyFilter,
	type EnemySort,
	type EnemySubTab,
	type EntityStatVM,
	enemyAccent,
	enemyKindLabel,
	formatBand,
	formatCooldown,
	liveEnemies,
	matchesEnemySearch,
	sortEnemyRows
} from './codex-display';
import { type LevelRange, levelRange, spawnShare, zoneTotalWeight } from './enemy-level';
import type { StatEntityKind } from '../stats/statistics-view.svelte';

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

/** The initial-selection payload a deep-link into the Enemies tab can carry. */
export interface EnemiesTabPayload {
	enemyId?: number;
	sub?: EnemySubTab;
}

export class EnemiesTabView {
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
	/** Enemy-table search query (matched against name, kind and spawn zones). */
	search = $state('');
	/** Enemy-table sort metric. */
	sort = $state<EnemySort>('level');

	/** Projects the player's per-entity statistics (owned by the CodexView orchestrator, which fetches
	 *  them once for every tab's dossier). */
	private readonly statVMsFor: (kind: StatEntityKind, id: number) => EntityStatVM[];

	constructor(
		payload: EnemiesTabPayload | undefined,
		statVMsFor: (kind: StatEntityKind, id: number) => EntityStatVM[]
	) {
		this.statVMsFor = statVMsFor;
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

	readonly enemies = $derived(liveEnemies(staticData.enemies));

	/** Immutable per-enemy view-models — depends only on reference data (enemies + zones), so the heavy
	 *  projection (level band, the zone-name search haystack incl. the per-boss `zones.find`) is built
	 *  once and reused, rather than rebuilt on every search keystroke / sort / filter change. */
	readonly enemyProjections = $derived.by<EnemyRowVM[]>(() => {
		const zones = staticData.zones ?? [];
		return this.enemies.map((e) => {
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
				searchText: [e.name, enemyKindLabel(e.isBoss), ...zoneNames].join(' ').toLowerCase()
			};
		});
	});

	/** Enemy table rows: the immutable projections narrowed by the active filter + search query and
	 *  ordered by the active sort — the only work that reruns as the player types or re-sorts. The
	 *  filter/search/sort maths are the pure helpers in `codex-display`. */
	readonly enemyRows = $derived.by<EnemyRowVM[]>(() =>
		this.enemyProjections
			.filter((row) => this.filter === 'all' || (this.filter === 'boss' ? row.isBoss : !row.isBoss))
			.filter((row) => matchesEnemySearch(row, this.search))
			.sort(sortEnemyRows(this.sort))
	);

	/** Number of enemies shown under the active filter + search (the "N shown" readout). */
	readonly shownCount = $derived(this.enemyRows.length);

	/* ── selected enemy + dossier ──────────────────────────────────────────────── */

	/** The inspected enemy: an explicitly-requested id resolves even when retired (retirement keeps a
	 *  record's slot resolvable by design — see backend.md → _Reference Data_), so a cross-link into a
	 *  retired enemy opens it rather than silently falling back. With no resolvable explicit id, falls
	 *  back to the head of the visible rows, then the full live list. */
	readonly selectedEnemy = $derived.by<IEnemy | undefined>(() => {
		const explicit = (staticData.enemies ?? [])[this.selectedEnemyId];
		if (explicit) {
			return explicit;
		}
		const firstVisible = this.enemyRows[0];
		return (firstVisible && (staticData.enemies ?? [])[firstVisible.id]) ?? this.enemies[0];
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

	readonly enemySkillRows = $derived.by<EnemySkillVM[]>(() => {
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

	/** Every statistic that references the selected enemy (the dossier's "your record"). */
	readonly statistics = $derived.by<EntityStatVM[]>(() =>
		this.selectedEnemy ? this.statVMsFor('enemy', this.selectedEnemy.id) : []
	);

	/** Challenges scoped to the selected enemy, with progress from the shared challenge store. */
	readonly challenges = $derived.by<EnemyChallengeVM[]>(() => {
		const e = this.selectedEnemy;
		if (!e) {
			return [];
		}
		// eslint-disable-next-line svelte/prefer-svelte-reactivity -- transient lookup, not held state
		const progressById = new Map(playerChallenges.all.map((pc) => [pc.challengeId, pc]));
		return (
			(staticData.challenges ?? [])
				.filter((c) => c.entityType === EEntityType.Enemy && c.targetEntityId === e.id)
				// A retired challenge is out of circulation: hide it unless the player already completed it
				// (mirrors the Challenges screen's retired-unless-completed rule).
				.filter((c) => c.retiredAt == null || progressById.get(c.id)?.completed)
				.map((c) => {
					const pc = progressById.get(c.id);
					const progress = pc?.progress ?? 0;
					const completed = pc?.completed ?? false;
					const comparison = comparisonFor(c.challengeTypeId, staticData.challengeTypes);
					const prog = progressInfo(c.progressGoal, comparison, progress);
					const progressText = prog.atMost
						? prog.hasData
							? `${formatTime(prog.best)} best · ≤${formatTime(prog.target)}`
							: `no time yet · ≤${formatTime(prog.target)}`
						: c.progressGoal === 1
							? completed
								? 'done'
								: 'sealed'
							: `${Math.round(prog.value)}/${c.progressGoal}`;
					return {
						id: c.id,
						name: c.name,
						typeLabel: challengeTypeName(c.challengeTypeId, staticData.challengeTypes),
						accent: challengeTypeColor(c.challengeTypeId),
						progressText,
						completed
					};
				})
		);
	});

	/** Dossier sub-tabs — Challenges only when the enemy has related challenges. */
	readonly subTabs = $derived.by<SubTabVM[]>(() =>
		this.challenges.length > 0 ? [...SUB_TAB_DEFS, { key: 'challenges', label: 'Challenges' }] : SUB_TAB_DEFS
	);

	/* ── handlers ──────────────────────────────────────────────────────────────── */

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

	/** Resolve an enemy id against the full catalogue (an explicit id resolves even when retired),
	 *  falling back to the head of the live list. */
	private resolveEnemy(id: number): IEnemy | undefined {
		return (staticData.enemies ?? [])[id] ?? liveEnemies(staticData.enemies)[0];
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
