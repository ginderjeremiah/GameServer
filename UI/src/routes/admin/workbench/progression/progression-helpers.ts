import type { IProficiencyLevelModifier, IProficiencyLevelReward, ISkillPathContribution } from '$lib/api';
import { EAttribute, EModifierType } from '$lib/api';
import { canonicalEqual } from '../save-helpers';
import type { SelectOption } from '../entities/types';
import { NO_SEED_SKILL, type WorkbenchPath, type WorkbenchProficiency } from './types';

// ── Factories ──

/** A new, unsaved path with the strawman falloff default. */
export const newPath = (id: number): WorkbenchPath => ({
	id,
	name: '',
	description: '',
	falloffBase: 0.6,
	contributions: []
});

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
	seedSkillId: NO_SEED_SKILL,
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

/** Home-tier select options for a path's contribution rows (one per tier, by ordinal). */
export const homeTierOptions = (tiers: WorkbenchProficiency[]): SelectOption[] =>
	tiers.map((tier) => ({ value: tier.pathOrdinal, text: `T${tier.pathOrdinal} · ${tier.name || 'Untitled'}` }));

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

// ── Contribution falloff preview ──

/** Pull multiplier per tier of distance above a skill's home: `falloffBase^distance` (distance 0 = 1). */
export const falloffSteps = (falloffBase: number, count = 3): { distance: number; factor: number }[] =>
	Array.from({ length: count }, (_unused, distance) => ({ distance, factor: Math.pow(falloffBase, distance) }));

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
	if (!(path.falloffBase > 0)) {
		warnings.push('Falloff base must be greater than zero');
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
	falloffBase: path.falloffBase,
	contributions: [] as ISkillPathContribution[],
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
	seedSkillId: prof.seedSkillId === NO_SEED_SKILL ? undefined : prof.seedSkillId,
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
