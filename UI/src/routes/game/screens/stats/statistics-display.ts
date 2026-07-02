/* statistics-display.ts — presentation helpers for the Statistics screen:
   value formatting per unit, and the category / breakdown-kind accent hues.

   Colours reference the existing themeable `var(--…)` tokens rather than
   hard-coding hex, so the screen restyles with the theme. The four stat
   categories and the entity-kind breakdowns reuse the shared accent palette
   (the same hues the attribute/log tokens use), so no new variables are
   introduced. The damage-type breakdown's per-row colour instead comes from
   the `--dmg-*` tokens via `damageTypeKeyColor` (see StatCardRow.svelte). */

import type { StatBreakdownKind, StatCategory, StatUnit } from './statistics-view.svelte';

/* ── value formatting ─────────────────────────────────────────────────────── */

export function fmtCount(n: number): string {
	return Math.round(n).toLocaleString('en-US');
}

export function fmtDamage(n: number): string {
	return Math.round(n).toLocaleString('en-US');
}

/** Formats a duration given in seconds as `1.5s` / `2m 05s` / `1h 03m`. */
export function fmtTime(seconds: number): string {
	if (seconds < 60) {
		return `${seconds.toFixed(seconds < 10 ? 1 : 0)}s`;
	}
	const m = Math.floor(seconds / 60);
	const rs = Math.round(seconds % 60);
	if (m < 60) {
		return `${m}m ${String(rs).padStart(2, '0')}s`;
	}
	const h = Math.floor(m / 60);
	const rm = m % 60;
	return `${h}h ${String(rm).padStart(2, '0')}m`;
}

export function fmtValue(value: number, unit: StatUnit): string {
	return unit === 'time' ? fmtTime(value) : unit === 'damage' ? fmtDamage(value) : fmtCount(value);
}

/* ── category + kind accents (reference existing theme tokens) ─────────────── */

const CATEGORY_COLOR: Record<StatCategory, string> = {
	combat: 'var(--log-enemy)',
	survival: 'var(--success)',
	exploration: 'var(--accent)',
	time: 'var(--warning)'
};

const CATEGORY_LABEL: Record<StatCategory, string> = {
	combat: 'Combat',
	survival: 'Survival',
	exploration: 'Exploration',
	time: 'Time'
};

export const statCategoryColor = (cat: StatCategory): string => CATEGORY_COLOR[cat];
export const statCategoryLabel = (cat: StatCategory): string => CATEGORY_LABEL[cat];

const KIND_COLOR: Record<StatBreakdownKind, string> = {
	enemy: 'var(--log-enemy)',
	zone: 'var(--success)',
	skill: 'var(--accent)',
	// The card-head "kind" badge needs one representative hue; individual damage-type rows are
	// tinted per-type via `damageTypeKeyColor` instead (see StatCardRow.svelte).
	damageType: 'var(--dmg-elemental)'
};

const KIND_LABEL: Record<StatBreakdownKind, string> = {
	enemy: 'Enemy',
	zone: 'Zone',
	skill: 'Skill',
	damageType: 'Damage Type'
};

const KIND_PLURAL: Record<StatBreakdownKind, string> = {
	enemy: 'Enemies',
	zone: 'Zones',
	skill: 'Skills',
	damageType: 'Damage Types'
};

export const statKindColor = (kind: StatBreakdownKind): string => KIND_COLOR[kind];
export const statKindLabel = (kind: StatBreakdownKind): string => KIND_LABEL[kind];
export const statKindPlural = (kind: StatBreakdownKind): string => KIND_PLURAL[kind];
