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
   genuinely cross-tab — the active tab, the live player statistics + challenge-error state, the
   shared statistics query engine, and the cross-links between dossiers — and exposes the three tab
   instances (`enemiesTab`/`zonesTab`/`skillsTab`) directly; consuming components read/call through
   the owning tab instance (e.g. `view.enemiesTab.selectedEnemy`) rather than a flat pass-through API. */

import { staticData, statistics } from '$stores';
import { CODEX_TABS, type CodexTab, type EnemySubTab, type EntityStatVM, tabAccent, tabLabel } from './codex-display';
import { EnemiesTabView, type EnemyRowVM } from './enemies-tab-view.svelte';
import { SkillsTabView } from './skills-tab-view.svelte';
import { ZonesTabView, type ZoneProjectionVM } from './zones-tab-view.svelte';
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

	readonly enemiesTab: EnemiesTabView;
	readonly zonesTab: ZonesTabView;
	readonly skillsTab: SkillsTabView;

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
