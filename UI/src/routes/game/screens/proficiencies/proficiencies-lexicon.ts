/* Proficiencies screen — "The Lexicon": the pure logic core.

   Reference data (`staticData.proficiencies` + `staticData.paths`) and the player's progress
   (`playerProficiencies`) are composed into per-path **spine** view-models: each path is a linear chain of
   tiers (proficiencies) ordered by `pathOrdinal`. This module is framework-free so the whole state machine is
   unit-testable without rendering; the reactive `ProficienciesView` (proficiencies-view.svelte.ts) only
   wires these functions to the live stores and owns selection.

   Visibility is a client-side render choice (spike #982 decision 14): locked tiers are simply not drawn
   (no teasers). The unlocked set is derived from player levels + the path structure the client already
   holds — within a path a tier reveals once the tier before it (by `pathOrdinal`) is maxed. A tampered
   client reading ahead is an accepted non-goal. */

import type { DescribedTooltipController } from '$components/tooltip/tooltip-hover';
import type {
	IPath,
	IPlayerProficiency,
	IProficiency,
	IProficiencyLevelModifier,
	IProficiencyLevelReward,
	ISkillPathContribution
} from '$lib/api';

/** The visible state of a tier on its path. Hidden/locked tiers are absent from the view-model entirely
 *  (decision 14), so this enumerates only the drawn states. */
export type TierState = 'unlocked' | 'training' | 'maxed';

/** A tier's "word of power" decipher stage, derived from `level` vs `maxLevel` — both thresholds are
 *  derived, not stored: pronunciation is learned at `ceil(maxLevel / 2)`, the full translation at
 *  `maxLevel`. */
export type DecipherStage = 'undeciphered' | 'pronunciation' | 'translated';

/** A single tier (proficiency) on a path's spine. */
export interface TierView {
	id: number;
	name: string;
	pathOrdinal: number;
	level: number;
	maxLevel: number;
	/** Residual XP within the current level (a maxed tier banks none). */
	xp: number;
	/** XP required to advance from the current level; 0 once maxed. */
	xpForNext: number;
	state: TierState;
	/** The path's current frontier — the lowest un-maxed visible tier (the one XP routes to). */
	frontier: boolean;
	/** Levels (1..maxLevel) that grant a reward — the milestones drawn as diamonds on the spine's pip
	 *  track. Distinct and ascending, derived from the authored `levelRewards`. */
	milestoneLevels: number[];
	/** The authored per-level attribute payouts (the increments granted at a level), carried raw so the
	 *  inspector can format the per-level breakdown ladder via the shared attribute-modifier formatters. */
	levelModifiers: IProficiencyLevelModifier[];
	/** The authored milestone reward skills (`{ level, rewardSkillId }`), carried raw so the inspector can
	 *  resolve and label the skill each milestone grants. */
	levelRewards: IProficiencyLevelReward[];
	decipher: DecipherStage;
	/** The romanized word of power (rendered as conlang glyphs by the spine/rail). */
	word: string;
	pronunciation: string;
	translation: string;
	iconPath: string;
}

/** A discovered path, rendered as a rail entry and an ordered spine of visible tiers. */
export interface PathView {
	id: number;
	name: string;
	/** The rail reuses the root tier's word/icon — there is no path-level word. */
	word: string;
	iconPath: string;
	/** The skills that train this path (`{ skillId, homeTier, weight }`), for the inspector's "Trained by"
	 *  chips. Path-level (a skill feeds the path, not an individual tier — spike #982 decision 12). */
	contributions: ISkillPathContribution[];
	/** Visible tiers in ascending `pathOrdinal` (root first); the spine reverses for display. */
	tiers: TierView[];
}

/** The imperative controller for the screen's single shared word-of-power tooltip — driven by the
 *  generic `tooltipHover` action with a {@link TierView} payload. The Proficiencies screen owns the panel
 *  and builds this, then passes it down to the spine cards / inspector (mirroring the challenges reward
 *  tooltip) so they don't thread hover handlers back up. */
export type WordTooltipController = DescribedTooltipController<TierView>;

/** The decipher stage for a tier at `level` of `maxLevel`: `undeciphered` below `ceil(maxLevel / 2)`,
 *  `pronunciation` from there up to (but not including) `maxLevel`, `translated` at `maxLevel`. */
export function decipherStage(level: number, maxLevel: number): DecipherStage {
	if (level >= maxLevel) {
		return 'translated';
	}
	if (level >= Math.ceil(maxLevel / 2)) {
		return 'pronunciation';
	}
	return 'undeciphered';
}

/** The XP required to advance from `level` to `level + 1`, from the authored curve params:
 *  `baseXp × xpGrowth^level`, rounded to the persisted XP scale (3 dp) so the threshold and the stored
 *  residual XP compare on the same grid — mirroring the backend `Proficiency.XpForLevel`. */
export function xpForLevel(baseXp: number, xpGrowth: number, level: number): number {
	return Math.round(baseXp * Math.pow(xpGrowth, level) * 1000) / 1000;
}

/** The distinct, ascending milestone levels of a proficiency (the levels that grant a reward), clamped
 *  to `1..maxLevel` so a stray authored reward can't draw a pip outside the track. */
export function milestoneLevels(prof: IProficiency): number[] {
	const levels = new Set<number>();
	for (const reward of prof.levelRewards) {
		if (reward.level >= 1 && reward.level <= prof.maxLevel) {
			levels.add(reward.level);
		}
	}
	return [...levels].sort((a, b) => a - b);
}

/** The tier a path opens to when selected: its frontier if it has one, otherwise the most-advanced
 *  (deepest) visible tier when the whole spine is mastered. */
export function representativeTier(path: PathView): TierView | undefined {
	return path.tiers.find((t) => t.frontier) ?? path.tiers.at(-1);
}

/** Whether the player's firing skills feed the path's frontier tier — any skill that fires in battle
 *  (a selected loadout skill or an innate item-granted skill) and contributes to the path at a home tier
 *  at or below the frontier (a skill never trains a tier below where it was acquired, so the distance is
 *  always ≥ 0; spike #982 decision 12). */
function hasContributingSkill(path: IPath, frontierOrdinal: number, firingSkills: ReadonlySet<number>): boolean {
	return path.contributions.some((c) => c.homeTier <= frontierOrdinal && firingSkills.has(c.skillId));
}

/**
 * Composes the proficiency/path reference data and the player's progress into the per-path spine view-
 * models the screen renders — the pure logic core.
 *
 * Retired paths and retired proficiencies are excluded (they are out of circulation); a path with no
 * visible tier (its root not yet discovered) is dropped, so the result is exactly the **discovered**
 * paths, ordered by id. Within each path the visible tiers form the contiguous reachable prefix from the
 * root, in ascending `pathOrdinal`.
 */
export function buildLexicon(
	proficiencies: readonly IProficiency[],
	paths: readonly IPath[],
	player: readonly IPlayerProficiency[],
	firingSkills: readonly number[]
): PathView[] {
	// Index the player's progress; an absent proficiency is unopened (level 0, no row).
	const levelById = new Map<number, number>();
	const xpById = new Map<number, number>();
	const openedIds = new Set<number>();
	for (const row of player) {
		levelById.set(row.proficiencyId, row.level);
		xpById.set(row.proficiencyId, row.xp);
		openedIds.add(row.proficiencyId);
	}

	// A tier is maxed once its level reaches the authored cap (only an opened tier can have a non-zero
	// level, so no `opened` check is needed).
	const maxedIds = new Set<number>();
	for (const prof of proficiencies) {
		if ((levelById.get(prof.id) ?? 0) >= prof.maxLevel) {
			maxedIds.add(prof.id);
		}
	}

	// Group live proficiencies under their live path.
	const pathById = new Map<number, IPath>();
	for (const path of paths) {
		if (!path.retiredAt) {
			pathById.set(path.id, path);
		}
	}
	const tiersByPath = new Map<number, IProficiency[]>();
	for (const prof of proficiencies) {
		if (prof.retiredAt || !pathById.has(prof.pathId)) {
			continue;
		}
		const tiers = tiersByPath.get(prof.pathId);
		if (tiers) {
			tiers.push(prof);
		} else {
			tiersByPath.set(prof.pathId, [prof]);
		}
	}

	const firingSet = new Set(firingSkills);
	const result: PathView[] = [];
	for (const [pathId, tierProfs] of tiersByPath) {
		const path = pathById.get(pathId);
		if (!path) {
			continue;
		}
		const ordered = [...tierProfs].sort((a, b) => a.pathOrdinal - b.pathOrdinal);
		const tiers = derivePathSpine(path, ordered, levelById, xpById, maxedIds, openedIds, firingSet);
		if (tiers.length === 0) {
			continue;
		}
		// A non-empty spine always starts at the root (a non-root tier is hidden until its predecessor
		// maxes), so the rail reuses tiers[0]'s word/icon.
		result.push({
			id: path.id,
			name: path.name,
			word: tiers[0].word,
			iconPath: tiers[0].iconPath,
			contributions: path.contributions,
			tiers
		});
	}
	result.sort((a, b) => a.id - b.id);
	return result;
}

/** Derives the visible spine for one path: the reachable prefix from the root, each tier carrying its
 *  derived state, decipher stage, and progress. `ordered` must be the path's proficiencies ascending by
 *  `pathOrdinal`. */
function derivePathSpine(
	path: IPath,
	ordered: readonly IProficiency[],
	levelById: ReadonlyMap<number, number>,
	xpById: ReadonlyMap<number, number>,
	maxedIds: ReadonlySet<number>,
	openedIds: ReadonlySet<number>,
	firingSkills: ReadonlySet<number>
): TierView[] {
	// Reachability builds a contiguous prefix from the root — a tier whose predecessor is un-maxed (so
	// hidden) keeps every deeper tier hidden too. The root reveals when the path is discovered (it opens by
	// acquiring a contributing skill, so it needs a player row); a deeper tier reveals once the tier before
	// it (by `pathOrdinal`) is maxed.
	const visible: IProficiency[] = [];
	for (let i = 0; i < ordered.length; i++) {
		const prof = ordered[i];
		const reachable = i === 0 ? openedIds.has(prof.id) : maxedIds.has(ordered[i - 1].id);
		if (!reachable) {
			break;
		}
		visible.push(prof);
	}

	// The frontier is the lowest-ordinal visible tier that is not yet maxed (XP routes here).
	const frontier = visible.find((prof) => (levelById.get(prof.id) ?? 0) < prof.maxLevel);

	return visible.map((prof) => {
		const level = levelById.get(prof.id) ?? 0;
		const maxed = level >= prof.maxLevel;
		const isFrontier = frontier?.id === prof.id;
		let state: TierState;
		if (maxed) {
			state = 'maxed';
		} else if (isFrontier && hasContributingSkill(path, prof.pathOrdinal, firingSkills)) {
			state = 'training';
		} else {
			state = 'unlocked';
		}
		return {
			id: prof.id,
			name: prof.name,
			pathOrdinal: prof.pathOrdinal,
			level,
			maxLevel: prof.maxLevel,
			xp: xpById.get(prof.id) ?? 0,
			xpForNext: maxed ? 0 : xpForLevel(prof.baseXp, prof.xpGrowth, level),
			state,
			frontier: isFrontier,
			milestoneLevels: milestoneLevels(prof),
			levelModifiers: prof.levelModifiers,
			levelRewards: prof.levelRewards,
			decipher: decipherStage(level, prof.maxLevel),
			word: prof.word,
			pronunciation: prof.pronunciation,
			translation: prof.translation,
			iconPath: prof.iconPath
		};
	});
}
