/* Codex screen — a read-only reference glossary of the game's enemies, zones and skills.

   The Enemies tab is a master/detail: a filterable enemy table beside a dossier with Attributes
   (live level-scaled stats + a "show scaling" breakdown), Statistics (the player's per-enemy record),
   Skills, Spawns and Challenges sub-tabs — the Skills/Spawns rows cross-link into the Skills/Zones
   tabs. The Zones tab is a progression rail (a status dot per zone:
   cleared / unlocked / locked) beside a zone dossier — level band, spawn pool, boss card, spawn table
   and unlock condition — whose boss card and spawn rows cross-link into the enemy dossier. The Skills
   tab is a master/detail skill catalogue: a skill table (base damage / cooldown / used-by count)
   beside a dossier — base damage, cooldown, how to obtain the skill (derived from actual item-grant
   references, with the acquisition flag wording the enemy-only / not-obtainable case), the
   attributes it scales with, its authored effects (via the shared
   `$lib/common/skill-effect-display` helper) and the enemies that use it, which cross-link into the
   enemy dossier. The data is all live reference/runtime data — the screen reuses the real
   `BattleAttributes` enemy build for stat scaling (`$lib/common/enemy-attributes`), the Statistics
   screen's per-entity query (`StatisticsData.statsForEntity`), per-zone clears
   (`statistics.isZoneCleared`) and the challenge progress store — rather than hard-coding any of it.
   Per-entity statistics live here now — every dossier (enemy, zone and skill) shows a "your record"
   section, and the Statistics screen deep-links an entity into the matching dossier instead of
   rendering its own in-place one.

   The reactive layer is composed from one class per tab (`EnemiesTabView`, `ZonesTabView`,
   `SkillsTabView`, each in its own `*-tab-view.svelte.ts` file) so every tab's derivations stay
   independently readable/testable. This CodexView is the thin orchestrator: it owns only what's
   genuinely cross-tab — the active tab, the live player statistics + challenge-error state,
   the shared statistics query engine, and the cross-links between dossiers — and every other
   property/method below simply delegates to the owning tab's instance, so consuming components (and
   the existing tests) keep reading `view.<x>` exactly as before. */

import { staticData, statistics } from '$stores';
import {
	CODEX_TABS,
	type CodexTab,
	type EnemyFilter,
	type EnemySort,
	type EnemySubTab,
	type EntityStatVM,
	tabAccent,
	tabLabel
} from './codex-display';
import {
	EnemiesTabView,
	type EnemyChallengeVM,
	type EnemyRowVM,
	type EnemySkillVM,
	type EnemySpawnVM,
	type SubTabVM
} from './enemies-tab-view.svelte';
import {
	SkillsTabView,
	type SkillEffectVM,
	type SkillProvenanceVM,
	type SkillRarityVM,
	type SkillRowVM,
	type SkillScalingVM,
	type SkillUserVM
} from './skills-tab-view.svelte';
import {
	ZonesTabView,
	type ZoneBossVM,
	type ZoneProjectionVM,
	type ZoneRowVM,
	type ZoneSpawnVM,
	type ZoneUnlockVM
} from './zones-tab-view.svelte';
import { fmtValue } from '../stats/statistics-display';
import {
	StatisticsData,
	type StatEntityKind,
	buildStatEntities,
	buildStatTypes
} from '../stats/statistics-view.svelte';

// Re-exported so importers of the pre-split `CodexView` module still resolve these tab-projection
// types (e.g. the CodexView test suite) without reaching into the per-tab files directly.
export type { EnemyRowVM, ZoneProjectionVM };

/** A one-shot payload handed to the Codex via the navigation store (e.g. from the Statistics screen). */
export interface CodexNavPayload {
	tab?: CodexTab;
	enemyId?: number;
	zoneId?: number;
	skillId?: number;
	sub?: EnemySubTab;
}

export interface CodexTabVM {
	key: CodexTab;
	label: string;
	count: number;
	accent: string;
	active: boolean;
}

/* ── reactive view-model ──────────────────────────────────────────────────── */

export class CodexView {
	/** Active top-level tab. */
	tab = $state<CodexTab>('enemies');

	/** The player's statistic values, read live from the shared store (fetched on mount) so a
	 *  background update — e.g. the fight screen's optimistic zone-clear — is reflected in every
	 *  dossier's "your record" section without remounting the Codex. */
	readonly stats = $derived(statistics.stats);
	statsLoading = $state(true);
	statsError = $state(false);
	/** Whether the mount-time challenge-progress fetch failed — surfaced so the enemy dossier's
	 *  Challenges sub-tab doesn't render zero-progress/sealed as if it were authoritative. */
	challengesError = $state(false);

	private readonly enemiesTab: EnemiesTabView;
	private readonly zonesTab: ZonesTabView;
	private readonly skillsTab: SkillsTabView;

	constructor(payload?: CodexNavPayload) {
		if (payload?.tab) {
			this.tab = payload.tab;
		}
		const projectStats = (kind: StatEntityKind, id: number): EntityStatVM[] => this.statVMsFor(kind, id);
		this.enemiesTab = new EnemiesTabView({ enemyId: payload?.enemyId, sub: payload?.sub }, projectStats);
		this.zonesTab = new ZonesTabView({ zoneId: payload?.zoneId }, projectStats);
		this.skillsTab = new SkillsTabView({ skillId: payload?.skillId }, projectStats);
	}

	/* ── shared statistics query (feeds every tab's "your record" dossier section) ──────────────── */

	/** The statistic query engine, rebuilt from the live reference data + fetched values. */
	readonly statData = $derived(
		new StatisticsData(buildStatTypes(staticData.statisticTypes ?? []), this.stats, buildStatEntities())
	);

	/** Project the statistics referencing an entity into dossier "your record" rows. */
	private statVMsFor(kind: StatEntityKind, id: number): EntityStatVM[] {
		return this.statData.statsForEntity(kind, id).map((info) => ({
			label: info.stat.name,
			value: fmtValue(info.value, info.stat.unit)
		}));
	}

	/* ── tabs ─────────────────────────────────────────────────────────────────── */

	/** Top-level tabs with live counts + section accents. */
	readonly tabs = $derived.by<CodexTabVM[]>(() => {
		const counts: Record<CodexTab, number> = {
			enemies: this.enemiesTab.enemies.length,
			zones: this.zonesTab.zones.length,
			skills: this.skillsTab.skillsCatalogue.length
		};
		return CODEX_TABS.map((key) => ({
			key,
			label: tabLabel(key),
			count: counts[key],
			accent: tabAccent(key),
			active: key === this.tab
		}));
	});

	selectTab(tab: CodexTab): void {
		this.tab = tab;
	}

	/* ── enemies tab (delegates to EnemiesTabView) ───────────────────────────────── */

	get enemies() {
		return this.enemiesTab.enemies;
	}
	get enemyProjections(): EnemyRowVM[] {
		return this.enemiesTab.enemyProjections;
	}
	get enemyRows(): EnemyRowVM[] {
		return this.enemiesTab.enemyRows;
	}
	get shownCount() {
		return this.enemiesTab.shownCount;
	}

	get selectedEnemyId() {
		return this.enemiesTab.selectedEnemyId;
	}
	set selectedEnemyId(id: number) {
		this.enemiesTab.selectedEnemyId = id;
	}
	get selectedEnemy() {
		return this.enemiesTab.selectedEnemy;
	}
	get range() {
		return this.enemiesTab.range;
	}
	get dossierAccent() {
		return this.enemiesTab.dossierAccent;
	}
	get dossierKind() {
		return this.enemiesTab.dossierKind;
	}
	get attributes() {
		return this.enemiesTab.attributes;
	}
	get maxPrimary() {
		return this.enemiesTab.maxPrimary;
	}
	get enemySkillRows(): EnemySkillVM[] {
		return this.enemiesTab.enemySkillRows;
	}
	get spawnHeading() {
		return this.enemiesTab.spawnHeading;
	}
	get spawns(): EnemySpawnVM[] {
		return this.enemiesTab.spawns;
	}
	get statistics(): EntityStatVM[] {
		return this.enemiesTab.statistics;
	}
	get challenges(): EnemyChallengeVM[] {
		return this.enemiesTab.challenges;
	}
	get subTabs(): SubTabVM[] {
		return this.enemiesTab.subTabs;
	}

	get sub() {
		return this.enemiesTab.sub;
	}
	get level() {
		return this.enemiesTab.level;
	}
	get scaling() {
		return this.enemiesTab.scaling;
	}
	get filter() {
		return this.enemiesTab.filter;
	}
	get search() {
		return this.enemiesTab.search;
	}
	set search(value: string) {
		this.enemiesTab.search = value;
	}
	get sort() {
		return this.enemiesTab.sort;
	}
	set sort(value: EnemySort) {
		this.enemiesTab.sort = value;
	}

	selectEnemy(id: number): void {
		this.enemiesTab.selectEnemy(id);
	}
	selectSub(sub: EnemySubTab): void {
		this.enemiesTab.selectSub(sub);
	}
	setLevel(level: number): void {
		this.enemiesTab.setLevel(level);
	}
	toggleScaling(): void {
		this.enemiesTab.toggleScaling();
	}
	setFilter(filter: EnemyFilter): void {
		this.enemiesTab.setFilter(filter);
	}

	/* ── zones tab (delegates to ZonesTabView) ───────────────────────────────────── */

	get zones() {
		return this.zonesTab.zones;
	}
	get zoneProjections(): ZoneProjectionVM[] {
		return this.zonesTab.zoneProjections;
	}
	get zoneRows(): ZoneRowVM[] {
		return this.zonesTab.zoneRows;
	}

	get selectedZoneId() {
		return this.zonesTab.selectedZoneId;
	}
	set selectedZoneId(id: number) {
		this.zonesTab.selectedZoneId = id;
	}
	get selectedZone() {
		return this.zonesTab.selectedZone;
	}
	get zoneBand() {
		return this.zonesTab.zoneBand;
	}
	get selectedZoneStatus() {
		return this.zonesTab.selectedZoneStatus;
	}
	get zoneBoss(): ZoneBossVM | null {
		return this.zonesTab.zoneBoss;
	}
	get zoneSpawns(): ZoneSpawnVM[] {
		return this.zonesTab.zoneSpawns;
	}
	get zoneSpawnCount() {
		return this.zonesTab.zoneSpawnCount;
	}
	get zoneUnlock(): ZoneUnlockVM | null {
		return this.zonesTab.zoneUnlock;
	}
	get zoneStatistics(): EntityStatVM[] {
		return this.zonesTab.zoneStatistics;
	}

	selectZone(id: number): void {
		this.zonesTab.selectZone(id);
	}

	/* ── skills tab (delegates to SkillsTabView) ─────────────────────────────────── */

	get skillsCatalogue() {
		return this.skillsTab.skillsCatalogue;
	}
	get skillRows(): SkillRowVM[] {
		return this.skillsTab.skillRows;
	}

	get selectedSkillId() {
		return this.skillsTab.selectedSkillId;
	}
	set selectedSkillId(id: number) {
		this.skillsTab.selectedSkillId = id;
	}
	get selectedSkill() {
		return this.skillsTab.selectedSkill;
	}
	get selectedSkillRarity(): SkillRarityVM | null {
		return this.skillsTab.selectedSkillRarity;
	}
	get skillScaling(): SkillScalingVM[] {
		return this.skillsTab.skillScaling;
	}
	get skillEffects(): SkillEffectVM[] {
		return this.skillsTab.skillEffects;
	}
	get skillUsedBy(): SkillUserVM[] {
		return this.skillsTab.skillUsedBy;
	}
	get skillProvenance(): SkillProvenanceVM {
		return this.skillsTab.skillProvenance;
	}
	get skillStatistics(): EntityStatVM[] {
		return this.skillsTab.skillStatistics;
	}

	selectSkill(id: number): void {
		this.skillsTab.selectSkill(id);
	}

	/* ── cross-tab links ──────────────────────────────────────────────────────── */

	/** Cross-link: jump to an enemy's dossier from the Zones / Skills tab (boss card, spawn / used-by row). */
	openEnemy(id: number): void {
		this.tab = 'enemies';
		this.enemiesTab.selectEnemy(id);
	}

	/** Cross-link: jump to a zone's dossier from the enemy dossier's Spawns rows. */
	openZone(id: number): void {
		this.tab = 'zones';
		this.zonesTab.selectZone(id);
	}

	/** Cross-link: jump to a skill's dossier from the enemy dossier's Skills rows. */
	openSkill(id: number): void {
		this.tab = 'skills';
		this.skillsTab.selectSkill(id);
	}
}
