/* Presentation helpers + shared string-literal types for the Codex screen. Colours reference the
   themeable `var(--…)` tokens rather than hard-coding hex (per the theming rule), so the screen
   restyles with the theme. Kept dependency-light (no store, no reactive state) so the view-model and
   its tests import the types/formatters without a cycle. */

import type { LevelRange } from './enemy-level';

/** Top-level Codex tab. `enemies`/`zones` are built; `skills` shows a placeholder. */
export type CodexTab = 'enemies' | 'zones' | 'skills';
/** A zone's progression status in the Codex rail. */
export type ZoneStatus = 'cleared' | 'unlocked' | 'locked';
/** Enemy dossier sub-tab. `challenges` only appears when the enemy has related challenges. */
export type EnemySubTab = 'attributes' | 'statistics' | 'skills' | 'spawns' | 'challenges';
/** Enemy-table filter chip. */
export type EnemyFilter = 'all' | 'normal' | 'boss';
/** Enemy-table sort metric. */
export type EnemySort = 'level' | 'name';

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

/** Status accent for a zone in the rail: a cleared zone reads in success green, an open-but-uncleared
 *  zone in the zone accent, a locked zone muted. */
export const zoneStatusColor = (status: ZoneStatus): string =>
	status === 'cleared' ? 'var(--success)' : status === 'unlocked' ? 'var(--accent)' : 'var(--text-muted)';

/** Title-cased status label for the zone rail / dossier. */
export const zoneStatusLabel = (status: ZoneStatus): string =>
	status === 'cleared' ? 'Cleared' : status === 'unlocked' ? 'Unlocked' : 'Locked';

/** A compact level band: `L18` for a fixed boss encounter, `18–28` for a ranged spawn. */
export const formatBand = (range: LevelRange): string => (range.fixed ? `L${range.min}` : `${range.min}–${range.max}`);

/** A cooldown in milliseconds as `—` (no cooldown / utility), `2s`, or `1.8s`. */
export function formatCooldown(ms: number): string {
	if (ms === 0) {
		return '—';
	}
	return ms % 1000 === 0 ? `${ms / 1000}s` : `${(ms / 1000).toFixed(1)}s`;
}

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
