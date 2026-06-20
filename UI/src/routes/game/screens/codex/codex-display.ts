/* Presentation helpers + shared string-literal types for the Codex screen. Colours reference the
   themeable `var(--…)` tokens rather than hard-coding hex (per the theming rule), so the screen
   restyles with the theme. Kept dependency-light (no store, no reactive state) so the view-model and
   its tests import the types/formatters without a cycle. */

import type { LevelRange } from './enemy-level';

/** Top-level Codex tab. Only `enemies` is built today; `zones`/`skills` show a placeholder. */
export type CodexTab = 'enemies' | 'zones' | 'skills';
/** Enemy dossier sub-tab. `challenges` only appears when the enemy has related challenges. */
export type EnemySubTab = 'attributes' | 'statistics' | 'skills' | 'spawns' | 'challenges';
/** Enemy-table filter chip. */
export type EnemyFilter = 'all' | 'normal' | 'boss';

export const CODEX_TABS: CodexTab[] = ['enemies', 'zones', 'skills'];

export const ENEMY_FILTERS: { key: EnemyFilter; label: string }[] = [
	{ key: 'all', label: 'All' },
	{ key: 'normal', label: 'Normal' },
	{ key: 'boss', label: 'Boss' }
];

/** Accent hue per top-level tab — the section's themed colour (enemy / zone / skill). */
export const tabAccent = (tab: CodexTab): string =>
	tab === 'enemies' ? 'var(--enemy-accent)' : tab === 'zones' ? 'var(--accent)' : 'var(--attr-intellect)';

/** Title-cased tab label. */
export const tabLabel = (tab: CodexTab): string => tab[0].toUpperCase() + tab.slice(1);

/** A normal enemy reads in salmon, a boss in gold. */
export const enemyAccent = (isBoss: boolean): string => (isBoss ? 'var(--boss-accent)' : 'var(--enemy-accent)');

export const enemyKindLabel = (isBoss: boolean): string => (isBoss ? 'Boss' : 'Enemy');

/** A compact level band: `L18` for a fixed boss encounter, `18–28` for a ranged spawn. */
export const formatBand = (range: LevelRange): string => (range.fixed ? `L${range.min}` : `${range.min}–${range.max}`);

/** A cooldown in milliseconds as `—` (no cooldown / utility), `2s`, or `1.8s`. */
export function formatCooldown(ms: number): string {
	if (ms === 0) {
		return '—';
	}
	return ms % 1000 === 0 ? `${ms / 1000}s` : `${(ms / 1000).toFixed(1)}s`;
}
