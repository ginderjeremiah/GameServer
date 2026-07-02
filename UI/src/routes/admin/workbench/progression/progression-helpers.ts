import type { IProficiencyLevelModifier, IProficiencyLevelReward } from '$lib/api';
import { EActivityKey, EAttribute, EModifierType } from '$lib/api';
import { canonicalEqual } from '../save-helpers';
import type { SelectOption } from '../entities/types';
import type { WorkbenchPath, WorkbenchProficiency } from './types';

// ── Factories ──

/** A new, unsaved path keyed on physical damage by default (the author then picks its activity key). */
export const newPath = (id: number): WorkbenchPath => ({
	id,
	name: '',
	description: '',
	activityKey: EActivityKey.Physical,
	designerNotes: ''
});

// ── Activity-key picker (path identity) ──

/** Combat-event activity keys (not damage types) — labelled by the quantity they train on. */
const ACTIVITY_EVENT_LABELS: Partial<Record<EActivityKey, string>> = {
	[EActivityKey.Crit]: 'Critical damage',
	[EActivityKey.Dodge]: 'Dodged damage',
	[EActivityKey.Heal]: 'Healing done',
	[EActivityKey.Reflect]: 'Reflected damage',
	[EActivityKey.Hex]: 'Vulnerability damage enabled',
	[EActivityKey.Momentum]: 'Ramp damage enabled'
};

/** Spell the damage-type stem of an activity-key name ("Dot" → "DoT"; others read as authored). */
const typeStemLabel = (stem: string): string => (stem === 'Dot' ? 'DoT' : stem);

/**
 * A path's activity key as a friendly label: a combat event by what it trains ("Critical damage"),
 * an incoming-book key suffixed "(resist)", or the bare damage-type stem for the output book.
 */
export const activityKeyLabel = (key: EActivityKey): string => {
	const event = ACTIVITY_EVENT_LABELS[key];
	if (event) {
		return event;
	}
	const name = EActivityKey[key];
	return name.endsWith('Resist') ? `${typeStemLabel(name.slice(0, -'Resist'.length))} (resist)` : typeStemLabel(name);
};

/** A labelled group of select options (rendered as an `<optgroup>`). */
export interface SelectOptionGroup {
	label: string;
	options: SelectOption[];
}

/**
 * Activity-key options for the path identity picker, grouped into the two books plus the combat events:
 * offense (damage dealt), combat events, and resistance (damage taken). Derived from the enum so an
 * appended offense key (e.g. a weapon-type leaf) shows up automatically under "Damage dealt".
 */
export const activityKeyGroups: SelectOptionGroup[] = (() => {
	const offense: SelectOption[] = [];
	const events: SelectOption[] = [];
	const resist: SelectOption[] = [];
	for (const [name, value] of Object.entries(EActivityKey)) {
		if (typeof value !== 'number') {
			continue;
		}
		const eventLabel = ACTIVITY_EVENT_LABELS[value as EActivityKey];
		if (eventLabel !== undefined) {
			events.push({ value, text: eventLabel });
		} else if (name.endsWith('Resist')) {
			resist.push({ value, text: typeStemLabel(name.slice(0, -'Resist'.length)) });
		} else {
			offense.push({ value, text: typeStemLabel(name) });
		}
	}
	return [
		{ label: 'Damage dealt', options: offense },
		{ label: 'Combat events', options: events },
		{ label: 'Damage taken — resistance', options: resist }
	];
})();

/** A new, unsaved proficiency (path tier) with the strawman cap/curve defaults. */
export const newProficiency = (id: number, pathId: number, pathOrdinal: number): WorkbenchProficiency => ({
	id,
	name: '',
	description: '',
	iconPath: '',
	word: '',
	pronunciation: '',
	translation: '',
	pathId,
	pathOrdinal,
	maxLevel: 10,
	baseXp: 100,
	xpGrowth: 1.4,
	designerNotes: '',
	levelModifiers: [],
	levelRewards: [],
	prerequisiteIds: []
});

// ── Tier layout ──

/** A path's tiers, ascending by ordinal. */
export const tiersOfPath = (profs: WorkbenchProficiency[], pathId: number): WorkbenchProficiency[] =>
	profs.filter((p) => p.pathId === pathId).sort((a, b) => a.pathOrdinal - b.pathOrdinal);

/** Reassign contiguous 0..n-1 ordinals to a list already in the desired display order. */
export const renumberTiers = (orderedTiers: WorkbenchProficiency[]): WorkbenchProficiency[] =>
	orderedTiers.map((tier, index) => ({ ...tier, pathOrdinal: index }));

/** True when two tiers in the list share an ordinal — the collision the backend rejects. */
export const hasTierCollision = (tiers: WorkbenchProficiency[]): boolean => {
	const seen = new Set<number>();
	for (const tier of tiers) {
		if (seen.has(tier.pathOrdinal)) {
			return true;
		}
		seen.add(tier.pathOrdinal);
	}
	return false;
};

// ── XP curve ──

/** Per-level cost: `baseXp × xpGrowth^(n-1)` for n in 1..maxLevel (derived, not stored). */
export const xpCostCurve = (baseXp: number, xpGrowth: number, maxLevel: number): number[] => {
	const levels = Math.max(0, Math.floor(maxLevel));
	const costs: number[] = [];
	for (let level = 1; level <= levels; level++) {
		costs.push(Math.round(baseXp * Math.pow(xpGrowth, level - 1)));
	}
	return costs;
};

/** Cumulative XP required to reach a level (sum of the costs of every level below it). */
export const cumulativeXp = (baseXp: number, xpGrowth: number, level: number): number => {
	let total = 0;
	for (let n = 1; n < level; n++) {
		total += Math.round(baseXp * Math.pow(xpGrowth, n - 1));
	}
	return total;
};

// ── Conlang decipher (thresholds derived, never stored) ──

/** Pronunciation reveals at `ceil(maxLevel/2)`, translation at `maxLevel`. */
export const decipherThresholds = (maxLevel: number): { pronunciation: number; translation: number } => {
	const max = Math.max(1, Math.floor(maxLevel));
	return { pronunciation: Math.ceil(max / 2), translation: max };
};

// ── Milestone (payout) projection over the two backend collections ──

/** Levels that carry a payout (a modifier and/or a reward), ascending. */
export const payoutLevels = (prof: WorkbenchProficiency): number[] => {
	const levels = new Set<number>();
	for (const modifier of prof.levelModifiers) {
		levels.add(modifier.level);
	}
	for (const reward of prof.levelRewards) {
		levels.add(reward.level);
	}
	return [...levels].sort((a, b) => a - b);
};

export const modifiersAtLevel = (prof: WorkbenchProficiency, level: number): IProficiencyLevelModifier[] =>
	prof.levelModifiers.filter((modifier) => modifier.level === level);

export const rewardAtLevel = (prof: WorkbenchProficiency, level: number): IProficiencyLevelReward | undefined =>
	prof.levelRewards.find((reward) => reward.level === level);

/** A level is a "milestone" when it grants a reward skill (vs. a plain per-level attribute bonus). */
export const isMilestoneLevel = (prof: WorkbenchProficiency, level: number): boolean =>
	rewardAtLevel(prof, level) !== undefined;

/** A blank attribute modifier for a new payout row. */
export const blankModifier = (level: number): IProficiencyLevelModifier => ({
	level,
	attributeId: EAttribute.Strength,
	modifierTypeId: EModifierType.Additive,
	amount: 0
});

// ── Validation (mirrors the backend's named rejections so the editor flags before saving) ──

export const pathWarnings = (path: WorkbenchPath): string[] => {
	const warnings: string[] = [];
	if (!path.name.trim()) {
		warnings.push('Missing name');
	}
	return warnings;
};

export const proficiencyWarnings = (prof: WorkbenchProficiency): string[] => {
	const warnings: string[] = [];
	if (!prof.name.trim()) {
		warnings.push('Missing name');
	}
	if (!prof.word.trim() || !prof.pronunciation.trim() || !prof.translation.trim()) {
		warnings.push('Missing words of power');
	}
	if (!prof.iconPath.trim()) {
		warnings.push('Missing icon path');
	}
	if (prof.maxLevel < 1) {
		warnings.push('Max level must be at least 1');
	}
	if (!(prof.baseXp > 0) || !(prof.xpGrowth > 0)) {
		warnings.push('XP curve must be positive');
	}
	for (const modifier of prof.levelModifiers) {
		if (modifier.level < 0 || modifier.level > prof.maxLevel) {
			warnings.push(`Modifier level ${modifier.level} out of range`);
			break;
		}
	}
	for (const reward of prof.levelRewards) {
		if (reward.level < 1 || reward.level > prof.maxLevel) {
			warnings.push(`Reward level ${reward.level} out of range`);
			break;
		}
	}
	return warnings;
};

// ── Persisted DTOs (identity-level Add/Edit; child collections go through their own endpoints) ──

export const pathIdentityDto = (path: WorkbenchPath) => ({
	id: path.id,
	name: path.name,
	description: path.description,
	activityKey: path.activityKey,
	designerNotes: path.designerNotes,
	retiredAt: path.retiredAt
});

export const profIdentityDto = (prof: WorkbenchProficiency) => ({
	id: prof.id,
	name: prof.name,
	description: prof.description,
	iconPath: prof.iconPath,
	word: prof.word,
	pronunciation: prof.pronunciation,
	translation: prof.translation,
	pathId: prof.pathId,
	pathOrdinal: prof.pathOrdinal,
	maxLevel: prof.maxLevel,
	baseXp: prof.baseXp,
	xpGrowth: prof.xpGrowth,
	designerNotes: prof.designerNotes,
	levelModifiers: [] as IProficiencyLevelModifier[],
	levelRewards: [] as IProficiencyLevelReward[],
	prerequisiteIds: [] as number[],
	retiredAt: prof.retiredAt
});

// ── Diffing & new-id resolution (shared shape with the generic save pipeline) ──

export interface CatalogueDiff<T> {
	added: T[];
	modified: { record: T; baseline: T }[];
}

/**
 * Split a catalogue against its baseline into added (no baseline) and modified (differs from baseline
 * in any way — identity or child collection). Retire is an edit, so there is no delete set; never-saved
 * records that are dropped simply never appear in `current`.
 */
export const diffCatalogue = <T extends { id: number }>(current: T[], baseline: T[]): CatalogueDiff<T> => {
	const baseMap = new Map(baseline.map((record) => [record.id, record]));
	const added: T[] = [];
	const modified: { record: T; baseline: T }[] = [];
	for (const record of current) {
		const base = baseMap.get(record.id);
		if (!base) {
			added.push(record);
		} else if (!canonicalEqual(record, base)) {
			modified.push({ record, baseline: base });
		}
	}
	return { added, modified };
};

/**
 * Map the local (negative) ids of added records to their persisted ids. The backend appends adds in
 * send order, so the k-th added record maps to the k-th lowest id absent from the pre-save set.
 */
export const resolveNewIds = (
	fresh: { id: number }[],
	existingIds: Iterable<number>,
	added: { id: number }[]
): Map<number, number> => {
	const existing = new Set(existingIds);
	const newlyPersisted = fresh.filter((record) => !existing.has(record.id)).sort((a, b) => a.id - b.id);
	const idFor = new Map<number, number>();
	added.forEach((record, index) => {
		const persisted = newlyPersisted[index];
		if (persisted) {
			idFor.set(record.id, persisted.id);
		}
	});
	return idFor;
};

/** Resolve a possibly-local id through the new-id map (returns the input when already persisted). */
export const resolveId = (id: number, idMap: Map<number, number>): number => idMap.get(id) ?? id;
