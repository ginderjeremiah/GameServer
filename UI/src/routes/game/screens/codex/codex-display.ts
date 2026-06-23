/* Presentation helpers + shared string-literal types for the Codex screen. Colours reference the
   themeable `var(--…)` tokens rather than hard-coding hex (per the theming rule), so the screen
   restyles with the theme. Kept store-free (no reactive state) so the view-model and its tests import
   the types/formatters without a cycle. */

import { formatNum } from '$lib/common';
import type { LevelRange } from './enemy-level';
import type { SkillAcquisitionStatus } from './skill-provenance';

/** Top-level Codex tab — Enemies, Zones and Skills are all built. */
export type CodexTab = 'enemies' | 'zones' | 'skills';
/** Enemy dossier sub-tab. `challenges` only appears when the enemy has related challenges. */
export type EnemySubTab = 'attributes' | 'statistics' | 'skills' | 'spawns' | 'challenges';
/** Enemy-table filter chip. */
export type EnemyFilter = 'all' | 'normal' | 'boss';
/** Enemy-table sort metric. */
export type EnemySort = 'level' | 'name';
/** A zone's progression status on the Zones rail. */
export type ZoneStatus = 'cleared' | 'unlocked' | 'locked';

export const CODEX_TABS: CodexTab[] = ['enemies', 'zones', 'skills'];

export const ENEMY_FILTERS: { key: EnemyFilter; label: string }[] = [
	{ key: 'all', label: 'All' },
	{ key: 'normal', label: 'Normal' },
	{ key: 'boss', label: 'Boss' }
];

export const ENEMY_SORTS: { key: EnemySort; label: string }[] = [
	{ key: 'level', label: 'Level' },
	{ key: 'name', label: 'Name' }
];

/** The minimum shape the pure search/sort helpers need from an enemy row. */
export interface EnemySearchSortFields {
	name: string;
	/** Numeric sort key for the level metric (the band's low end). */
	level: number;
	/** Pre-lowercased haystack: the enemy name, its kind, and the zones it appears in. */
	searchText: string;
}

/** Accent hue per top-level tab — the section's themed colour (enemy / zone / skill). */
export const tabAccent = (tab: CodexTab): string =>
	tab === 'enemies' ? 'var(--enemy-accent)' : tab === 'zones' ? 'var(--accent)' : 'var(--attr-intellect)';

/** Title-cased tab label. */
export const tabLabel = (tab: CodexTab): string => tab[0].toUpperCase() + tab.slice(1);

/** A normal enemy reads in salmon, a boss in gold. */
export const enemyAccent = (isBoss: boolean): string => (isBoss ? 'var(--boss-accent)' : 'var(--enemy-accent)');

export const enemyKindLabel = (isBoss: boolean): string => (isBoss ? 'Boss' : 'Enemy');

/* ── zone progression status ─────────────────────────────────────────────── */

/** Short status label for the Zones rail / dossier seal. */
export const ZONE_STATUS_LABELS: Record<ZoneStatus, string> = {
	cleared: 'Cleared',
	unlocked: 'Unlocked',
	locked: 'Locked'
};

/** The themed status colour: a cleared zone reads in success-green, an open zone in the zone accent,
 *  and a sealed zone dims to muted text. */
export const zoneStatusColor = (status: ZoneStatus): string =>
	status === 'cleared' ? 'var(--success)' : status === 'unlocked' ? 'var(--accent)' : 'var(--text-muted)';

/** A zone's progression status: a clear takes precedence, then a sealed unlock gate, else open. */
export const resolveZoneStatus = (cleared: boolean, locked: boolean): ZoneStatus =>
	cleared ? 'cleared' : locked ? 'locked' : 'unlocked';

/** A compact level band: `L18` for a fixed boss encounter, `18–28` for a ranged spawn. */
export const formatBand = (range: LevelRange): string => (range.fixed ? `L${range.min}` : `${range.min}–${range.max}`);

/** A cooldown in milliseconds as `—` (no cooldown / utility), `2s`, or `1.8s`. */
export function formatCooldown(ms: number): string {
	if (ms === 0) {
		return '—';
	}
	return ms % 1000 === 0 ? `${ms / 1000}s` : `${(ms / 1000).toFixed(1)}s`;
}

/** A skill's base damage for display: the formatted number, or `—` for a utility (zero-damage) skill. */
export function formatBaseDamage(baseDamage: number): string {
	return baseDamage > 0 ? formatNum(baseDamage) : '—';
}

/* ── skill provenance (how-to-obtain) display ──────────────────────────────── */

/** Lead-in for a concrete acquisition source. Items are the only concrete player source surfaced
 *  here (challenges no longer grant skills — spike #982). */
export const skillSourceLabel = (): string => 'Granted by';

/** Wording for the no-source acquisition cases (empty for an obtainable skill, which lists sources). */
export const SKILL_ACQUISITION_EMPTY: Record<SkillAcquisitionStatus, string> = {
	obtainable: '',
	'enemy-only': 'Enemy-only — encountered in battle, not obtainable by you.',
	unobtainable: 'Not currently obtainable.'
};

/* ── enemy-table search + sort (pure, unit-tested directly) ────────────────── */

/** Does an enemy row match the search query? An empty/whitespace query matches everything; otherwise
 *  the (already-lowercased) query must be a substring of the row's haystack (name + kind + zones). */
export function matchesEnemySearch(row: EnemySearchSortFields, query: string): boolean {
	const term = query.trim().toLowerCase();
	return term === '' || row.searchText.includes(term);
}

/** Comparator over enemy rows for the given sort metric. Level sorts ascending (low bands first),
 *  name sorts alphabetically; both fall back to name so the order is stable on ties. */
export function sortEnemyRows(sort: EnemySort): (a: EnemySearchSortFields, b: EnemySearchSortFields) => number {
	switch (sort) {
		case 'name':
			return (a, b) => a.name.localeCompare(b.name);
		case 'level':
		default:
			return (a, b) => a.level - b.level || a.name.localeCompare(b.name);
	}
}
