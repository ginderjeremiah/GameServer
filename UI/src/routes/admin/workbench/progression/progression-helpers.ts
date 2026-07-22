import type { IProficiencyLevelModifier, IProficiencyLevelReward } from '$lib/api';
import { EActivityKey, EAttribute, EModifierType } from '$lib/api';
import { activityKeyDisplay, type ActivityKeyKind } from '$lib/common';
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

/** A labelled group of select options (rendered as an `<optgroup>`). */
export interface SelectOptionGroup {
	label: string;
	options: SelectOption[];
}

/**
 * Activity-key options for the path identity picker, grouped into the two books plus the combat events:
 * offense (damage dealt), combat events, and resistance (damage taken). Classification and labels come
 * from the shared `activityKeyDisplay`, so an appended enum key shows up automatically in its group.
 */
export const activityKeyGroups: SelectOptionGroup[] = (() => {
	const byKind: Record<ActivityKeyKind, SelectOption[]> = { offense: [], event: [], resist: [] };
	for (const value of Object.values(EActivityKey)) {
		if (typeof value !== 'number') {
			continue;
		}
		const { kind, label } = activityKeyDisplay(value);
		byKind[kind].push({ value, text: label });
	}
	return [
		{ label: 'Damage dealt', options: byKind.offense },
		{ label: 'Combat events', options: byKind.event },
		{ label: 'Damage taken — resistance', options: byKind.resist }
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

/**
 * A tier carrying prerequisites that isn't (or would no longer be) its path's root tier — the same rule
 * AdminProficiencies.ValidatePrerequisiteIds/FindPrerequisiteRootViolation hard-rejects a save over.
 * Root-only gating is asserted at authoring time (#2236); a tier reorder can strand an already-gated
 * root tier off ordinal 0 without ever touching prerequisiteIds, so this must be checked against the
 * about-to-be-saved pathOrdinal, not just at the point prerequisites are edited.
 */
export const prerequisiteRootWarnings = (prof: WorkbenchProficiency): string[] =>
	prof.pathOrdinal !== 0 && prof.prerequisiteIds.length > 0 ? ['Prerequisites are only allowed on a root tier'] : [];

/**
 * An out-of-range modifier/reward level, checked against the tier's own (about-to-be-saved) MaxLevel.
 * This is the one proficiency condition the backend genuinely hard-rejects a save over — via
 * AdminProficiencies.FindShrunkenMaxLevelViolation (the identity edit, against a still-persisted
 * payout) or FindLevelOutOfRange (SetModifiers/SetRewards, against the tier's saved MaxLevel) — so it
 * doubles as {@link proficiencyBlockingWarnings}, unlike the rest of {@link proficiencyWarnings}.
 */
export const levelRangeWarnings = (prof: WorkbenchProficiency): string[] => {
	const warnings: string[] = [];
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
	return [...warnings, ...levelRangeWarnings(prof), ...prerequisiteRootWarnings(prof)];
};

/**
 * The subset of {@link proficiencyWarnings} the backend is known to hard-reject a save over — see
 * {@link levelRangeWarnings} and {@link prerequisiteRootWarnings}. `MaxLevel < 1` and a non-positive XP
 * curve stay advisory-only: neither `Contracts.Proficiency` nor `AdminProficiencies` rejects them, so
 * gating Save on them would block a save the backend would actually accept.
 */
export const proficiencyBlockingWarnings = (prof: WorkbenchProficiency): string[] => [
	...levelRangeWarnings(prof),
	...prerequisiteRootWarnings(prof)
];

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
