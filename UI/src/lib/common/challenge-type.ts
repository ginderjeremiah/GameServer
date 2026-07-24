import { EChallengeGoalComparison, EChallengeType, type IChallengeType } from '$lib/api';
import { normalizeText } from './functions';

/*
 * Single source of truth for challenge-type accent visuals. The hues are
 * declared as `--challenge-*` custom properties in `+layout.svelte` so they
 * remain themeable; these helpers only reference those variables (mirroring the
 * rarity helpers in `rarity.ts`). The per-type *label*, *blurb* and *unit* are
 * presentation strings local to the challenge screen, while the goal-comparison
 * direction (at-least vs at-most) is an intrinsic domain fact sourced from the
 * `Challenges/ChallengeTypes` reference data.
 */

/** Kebab key matching the `--challenge-*` custom properties (e.g. `enemies-killed`). */
const CHALLENGE_TYPE_KEY: Record<EChallengeType, string> = {
	[EChallengeType.EnemiesKilled]: 'enemies-killed',
	[EChallengeType.BossesDefeated]: 'bosses-defeated',
	[EChallengeType.ZonesCleared]: 'zones-cleared',
	[EChallengeType.TimeTrial]: 'time-trial',
	[EChallengeType.LevelReached]: 'level-reached',
	[EChallengeType.DamageDealt]: 'damage-dealt',
	[EChallengeType.BattlesWon]: 'battles-won',
	[EChallengeType.SkillsUsed]: 'skills-used',
	[EChallengeType.KillsByDamageType]: 'kills-by-damage-type'
};

/** Themeable challenge-type accent hue, e.g. `var(--challenge-enemies-killed)`. */
export const challengeTypeColor = (id: EChallengeType): string =>
	`var(--challenge-${CHALLENGE_TYPE_KEY[id] ?? 'enemies-killed'})`;

/**
 * The challenge type's display name — the authored name from the `ChallengeTypes` reference set
 * when available, falling back to the normalized enum name. The reference set is passed in (rather
 * than read from `$stores`) so this module stays free of a store dependency, mirroring the other
 * param-based `$lib/common` helpers.
 */
export const challengeTypeName = (id: EChallengeType, challengeTypes?: IChallengeType[]): string =>
	challengeTypes?.find((t) => t.id === id)?.name ?? normalizeText(EChallengeType[id] ?? '');

/** A challenge type's goal-comparison direction, from the `ChallengeTypes` reference set (default: at-least). */
export const comparisonFor = (id: EChallengeType, challengeTypes?: IChallengeType[]): EChallengeGoalComparison =>
	challengeTypes?.find((t) => t.id === id)?.goalComparison ?? EChallengeGoalComparison.AtLeast;

/**
 * Clamp an accumulating challenge's raw progress to its goal for display. Stored progress can
 * transiently exceed the goal in the window before a `ChallengeCompleted` push lands; every
 * progress readout (Challenges screen, Codex dossier) should route through this so they can't
 * diverge on how they handle it.
 */
export const clampChallengeProgress = (progress: number, goal: number): number => Math.min(progress, goal);

export interface ProgressInfo {
	/** Minimisation goal (lower is better, e.g. TimeTrial) vs accumulating goal. */
	atMost: boolean;
	/** 0–100 fill for the proximity/progress bar. */
	percent: number;
	/* accumulating (atLeast) */
	value: number;
	goal: number;
	/* minimisation (atMost) */
	best: number;
	target: number;
	/** atMost only: whether a qualifying value has been recorded yet. */
	hasData: boolean;
}

/**
 * Comparison-aware progress maths, branching on the challenge type's goal-comparison direction.
 * Every progress readout (Challenges screen, Codex dossier) should route through this so an
 * at-most (minimisation) challenge can't render as if it were an accumulating one elsewhere.
 */
export function progressInfo(goal: number, comparison: EChallengeGoalComparison, progress: number): ProgressInfo {
	if (comparison === EChallengeGoalComparison.AtMost) {
		// Stored progress is the player's current best (0 = no qualifying value yet).
		// Closeness to the target drives the bar; a 0→goal fill would be misleading.
		const best = progress;
		const hasData = best > 0;
		const percent = hasData ? Math.min(100, (goal / best) * 100) : 0;
		return { atMost: true, percent, value: 0, goal, best, target: goal, hasData };
	}
	const percent = Math.min(100, (progress / Math.max(1, goal)) * 100);
	return {
		atMost: false,
		percent,
		value: clampChallengeProgress(progress, goal),
		goal,
		best: 0,
		target: 0,
		hasData: true
	};
}

/** `90` -> `1:30`, `45` -> `45s`, `0`/missing -> `—`. An at-most progress readout's time formatting. */
export function formatTime(seconds: number): string {
	if (!seconds || seconds <= 0) {
		return '—';
	}
	const minutes = Math.floor(seconds / 60);
	const secs = Math.round(seconds % 60);
	return minutes > 0 ? `${minutes}:${String(secs).padStart(2, '0')}` : `${secs}s`;
}
