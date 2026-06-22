/* Codex screen — a read-only reference glossary of the game's enemies, zones and skills.

   The Enemies tab is a master/detail: a filterable enemy table beside a dossier with Attributes
   (live level-scaled stats + a "show scaling" breakdown), Statistics (the player's per-enemy record),
   Skills, Spawns and Challenges sub-tabs — the Skills/Spawns rows cross-link into the Skills/Zones
   tabs. The Zones tab is a progression rail (a status dot per zone:
   cleared / unlocked / locked) beside a zone dossier — level band, spawn pool, boss card, spawn table
   and unlock condition — whose boss card and spawn rows cross-link into the enemy dossier. The Skills
   tab is a master/detail skill catalogue: a skill table (base damage / cooldown / used-by count)
   beside a dossier — base damage, cooldown, how to obtain the skill (derived from actual challenge
   reward / item grant references, with the acquisition flag wording the enemy-only / not-obtainable
   case), the attributes it scales with, its authored effects (via the shared
   `$lib/common/skill-effect-display` helper) and the enemies that use it, which cross-link into the
   enemy dossier. The data is all live reference/runtime data — the screen reuses the real
   `BattleAttributes` enemy build for stat scaling (`$lib/common/enemy-attributes`), the Statistics
   screen's per-entity query (`StatisticsData.statsForEntity`), per-zone clears
   (`statistics.isZoneCleared`) and the challenge progress store — rather than hard-coding any of it.
   Per-entity statistics live here now — every dossier (enemy, zone and skill) shows a "your record"
   section, and the Statistics screen deep-links an entity into the matching dossier instead of
   rendering its own in-place one.

   The view-model only wires reactive state to the pure helpers; the projection maths live in
   `enemy-level` and the shared `$lib/common/enemy-attributes` (unit-tested directly). */

import { EEntityType, ERarity, type IEnemy, type IPlayerStatistic, type ISkill, type IZone } from '$lib/api';
import {
	type EnemyAttributes,
	attributeCode,
	attributeColor,
	attributeIsHarmful,
	attributeName,
	challengeTypeColor,
	challengeTypeName,
	describeEffect,
	effectDirectionColor,
	enemyAttributesAtLevel,
	formatNum,
	rarityColor
} from '$lib/common';
import { playerChallenges, staticData, statistics } from '$stores';
import { fmtValue } from '../stats/statistics-display';
import {
	type StatEntityKind,
	StatisticsData,
	buildStatEntities,
	buildStatTypes
} from '../stats/statistics-view.svelte';
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
	formatBaseDamage,
	formatCooldown,
	matchesEnemySearch,
	resolveZoneStatus,
	skillSourceLabel,
	sortEnemyRows,
	tabAccent,
	tabLabel,
	SKILL_ACQUISITION_EMPTY
} from './codex-display';
import { type LevelRange, levelRange, spawnShare, zoneTotalWeight } from './enemy-level';
import { type SkillAcquisitionStatus, resolveSkillProvenance } from './skill-provenance';

/** A one-shot payload handed to the Codex via the navigation store (e.g. from the Statistics screen). */
export interface CodexNavPayload {
	tab?: CodexTab;
	enemyId?: number;
	zoneId?: number;
	skillId?: number;
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

/** A single per-entity statistic row shown in a dossier's "Your record" section
 *  (shared by the enemy, zone and skill dossiers). */
export interface EntityStatVM {
	label: string;
	value: string;
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

export interface SkillRowVM {
	id: number;
	name: string;
	/** Base damage, or `—` for a utility skill. */
	baseDamageLabel: string;
	/** Cooldown, or `—` for an instant/utility skill. */
	cooldownLabel: string;
	/** How many (non-retired) enemies have this skill in their pool. */
	usedByCount: number;
	selected: boolean;
}

export interface SkillScalingVM {
	attributeId: number;
	name: string;
	code: string;
	/** Magnitude badge, e.g. `×1.5`. */
	multiplierLabel: string;
	color: string;
}

export interface SkillEffectVM {
	id: number;
	/** Signed/`×` magnitude badge, e.g. `+15` or `×0.5`. */
	magnitude: string;
	attributeName: string;
	/** `self` / `enemy`, the side the effect lands on. */
	targetLabel: string;
	/** Duration in seconds, e.g. `5s`. */
	duration: string;
	/** Themed buff/debuff accent for the magnitude. */
	color: string;
}

export interface SkillUserVM {
	enemyId: number;
	name: string;
	isBoss: boolean;
	accent: string;
}

/** A "how to obtain" source row — a challenge that rewards the skill or an item that grants it. */
export interface SkillSourceVM {
	kind: 'challenge' | 'item';
	id: number;
	/** "Rewarded by" / "Granted by" lead-in. */
	label: string;
	name: string;
	/** Themed accent: challenge → the dossier's intellect section accent; item → its rarity hue. */
	accent: string;
}

/** The skill dossier's acquisition section: concrete sources, plus the wording for the no-source case. */
export interface SkillProvenanceVM {
	status: SkillAcquisitionStatus;
	sources: SkillSourceVM[];
	/** Wording for the no-source case (empty when sources exist). */
	emptyLabel: string;
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

/** Live (non-retired) skills in catalogue order. */
const liveSkills = (skills: ISkill[] | undefined): ISkill[] => (skills ?? []).filter((s) => !s.retiredAt);

/* ── reactive view-model ──────────────────────────────────────────────────── */

export class CodexView {
	/** Active top-level tab. */
	tab = $state<CodexTab>('enemies');
	/** Inspected enemy id (falls back to the head of the list when unresolved). */
	selectedEnemyId = $state<number>(-1);
	/** Inspected zone id (falls back to the head of the rail when unresolved). */
	selectedZoneId = $state<number>(-1);
	/** Inspected skill id (falls back to the head of the catalogue when unresolved). */
	selectedSkillId = $state<number>(-1);
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
		// …and the skill table (the deep-link target, else the head of the catalogue).
		const initialSkill = this.resolveSkill(payload?.skillId ?? -1);
		if (initialSkill) {
			this.selectedSkillId = initialSkill.id;
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
			skills: this.skillsCatalogue.length
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

	/** The statistic query engine, rebuilt from the live reference data + fetched values. */
	readonly statData = $derived(
		new StatisticsData(buildStatTypes(staticData.statisticTypes ?? []), this.stats, buildStatEntities())
	);

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

	/** Every statistic that references the selected zone (the zone dossier's "your record"). */
	readonly zoneStatistics = $derived.by<EntityStatVM[]>(() =>
		this.selectedZone ? this.statVMsFor('zone', this.selectedZone.id) : []
	);

	/* ── skills catalogue (the reference skill table) ────────────────────────────── */

	readonly skillsCatalogue = $derived(liveSkills(staticData.skills));

	/** Skill table rows: name, base damage, cooldown and how many enemies use the skill. */
	readonly skillRows = $derived.by<SkillRowVM[]>(() => {
		const enemies = this.enemies;
		return this.skillsCatalogue.map((sk) => ({
			id: sk.id,
			name: sk.name,
			baseDamageLabel: formatBaseDamage(sk.baseDamage),
			cooldownLabel: formatCooldown(sk.cooldownMs),
			usedByCount: enemies.filter((e) => e.skillPool.includes(sk.id)).length,
			selected: sk.id === this.selectedSkillId
		}));
	});

	/* ── selected skill + dossier ─────────────────────────────────────────────────── */

	/** The inspected skill, falling back to the head of the catalogue. */
	readonly selectedSkill = $derived<ISkill | undefined>(
		this.skillsCatalogue.find((s) => s.id === this.selectedSkillId) ?? this.skillsCatalogue[0]
	);

	/** The attributes the selected skill's damage scales with, each tinted by its attribute accent. */
	readonly skillScaling = $derived.by<SkillScalingVM[]>(() => {
		const sk = this.selectedSkill;
		if (!sk) {
			return [];
		}
		return sk.damageMultipliers.map((m) => ({
			attributeId: m.attributeId,
			name: attributeName(m.attributeId, staticData.attributes),
			code: attributeCode(m.attributeId, staticData.attributes),
			multiplierLabel: `×${formatNum(m.multiplier)}`,
			color: attributeColor(m.attributeId)
		}));
	});

	/** The selected skill's authored effects, described via the shared helper so the wording and the
	 *  buff/debuff direction match every other surface that shows an effect. */
	readonly skillEffects = $derived.by<SkillEffectVM[]>(() => {
		const sk = this.selectedSkill;
		if (!sk) {
			return [];
		}
		return sk.effects.map((effect) => {
			const desc = describeEffect(
				effect,
				attributeName(effect.attributeId, staticData.attributes),
				attributeIsHarmful(effect.attributeId, staticData.attributes)
			);
			return {
				id: effect.id,
				magnitude: desc.magnitude,
				attributeName: desc.attributeName,
				targetLabel: desc.targetLabel,
				duration: desc.duration,
				color: effectDirectionColor(desc.direction)
			};
		});
	});

	/** The (non-retired) enemies whose skill pool includes the selected skill — pills that cross-link
	 *  into the enemy dossier. Empty for a player-only skill. */
	readonly skillUsedBy = $derived.by<SkillUserVM[]>(() => {
		const sk = this.selectedSkill;
		if (!sk) {
			return [];
		}
		return this.enemies
			.filter((e) => e.skillPool.includes(sk.id))
			.map((e) => ({
				enemyId: e.id,
				name: e.name,
				isBoss: e.isBoss,
				accent: enemyAccent(e.isBoss)
			}));
	});

	/** How the selected skill is obtained — derived from actual challenge/item references, with the
	 *  acquisition flag wording the no-source case. Item sources tint by the granting item's rarity. */
	readonly skillProvenance = $derived.by<SkillProvenanceVM>(() => {
		const sk = this.selectedSkill;
		if (!sk) {
			return { status: 'unobtainable', sources: [], emptyLabel: SKILL_ACQUISITION_EMPTY.unobtainable };
		}
		const allItems = staticData.items ?? [];
		const provenance = resolveSkillProvenance(
			sk,
			(staticData.challenges ?? []).filter((c) => !c.retiredAt),
			allItems.filter((i) => !i.retiredAt)
		);
		const sources = provenance.sources.map<SkillSourceVM>((src) => ({
			kind: src.kind,
			id: src.id,
			label: skillSourceLabel(src.kind),
			name: src.name,
			accent: src.kind === 'item' ? rarityColor(allItems[src.id]?.rarityId ?? ERarity.Common) : 'var(--attr-intellect)'
		}));
		return {
			status: provenance.status,
			sources,
			emptyLabel: sources.length > 0 ? '' : SKILL_ACQUISITION_EMPTY[provenance.status]
		};
	});

	/** Every statistic that references the selected skill (the skill dossier's "your record"). */
	readonly skillStatistics = $derived.by<EntityStatVM[]>(() =>
		this.selectedSkill ? this.statVMsFor('skill', this.selectedSkill.id) : []
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

	selectZone(id: number): void {
		this.selectedZoneId = id;
	}

	selectSkill(id: number): void {
		this.selectedSkillId = id;
	}

	/** Cross-link: jump to an enemy's dossier from the Zones / Skills tab (boss card, spawn / used-by row). */
	openEnemy(id: number): void {
		this.tab = 'enemies';
		this.selectEnemy(id);
	}

	/** Cross-link: jump to a zone's dossier from the enemy dossier's Spawns rows. */
	openZone(id: number): void {
		this.tab = 'zones';
		this.selectZone(id);
	}

	/** Cross-link: jump to a skill's dossier from the enemy dossier's Skills rows. */
	openSkill(id: number): void {
		this.tab = 'skills';
		this.selectSkill(id);
	}

	/* ── helpers (store reads, kept off the reactive graph for the constructor) ──── */

	/** Project the statistics referencing an entity into dossier "your record" rows. */
	private statVMsFor(kind: StatEntityKind, id: number): EntityStatVM[] {
		return this.statData.statsForEntity(kind, id).map((info) => ({
			label: info.stat.name,
			value: fmtValue(info.value, info.stat.unit)
		}));
	}

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

	/** Resolve a skill id against the catalogue, falling back to the head. */
	private resolveSkill(id: number): ISkill | undefined {
		const skills = liveSkills(staticData.skills);
		return skills.find((s) => s.id === id) ?? skills[0];
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
