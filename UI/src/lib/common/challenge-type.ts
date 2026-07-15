import { EChallengeType, type IChallengeType } from '$lib/api';
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
