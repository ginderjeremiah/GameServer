import { EChallengeType } from '$lib/api';

/*
 * Screen-local presentation strings for each challenge type. The accent *hues*
 * live in the theme (see $lib/common/challenge-type); these are the human-facing
 * blurb shown on the type hero banner and the noun used in a progress readout
 * ("12 / 50 kills"). The type's display name and intrinsic goal-comparison come
 * from the `Challenges/ChallengeTypes` reference data, not from here.
 */
export interface ChallengeTypeMeta {
	blurb: string;
	unit: string;
}

export const CHALLENGE_TYPE_META: Record<EChallengeType, ChallengeTypeMeta> = {
	[EChallengeType.EnemiesKilled]: { unit: 'kills', blurb: 'Cut down foes in the field.' },
	[EChallengeType.BossesDefeated]: { unit: 'bosses', blurb: 'Bring down the great threats.' },
	[EChallengeType.ZonesCleared]: { unit: 'zones', blurb: 'Sweep a region clean.' },
	[EChallengeType.TimeTrial]: { unit: 'time', blurb: 'Win faster than the mark.' },
	[EChallengeType.LevelReached]: { unit: 'level', blurb: 'Grow in power.' },
	[EChallengeType.DamageDealt]: { unit: 'damage', blurb: 'Pour on the hurt.' },
	[EChallengeType.BattlesWon]: { unit: 'wins', blurb: 'Come out on top.' },
	[EChallengeType.SkillsUsed]: { unit: 'casts', blurb: 'Master your abilities.' },
	[EChallengeType.KillsByDamageType]: { unit: 'kills', blurb: 'Master a school of damage.' }
};

/** A type's progress noun, with a sensible fallback for unknown types. */
export const challengeTypeUnit = (id: EChallengeType): string => CHALLENGE_TYPE_META[id]?.unit ?? '';

/** A type's hero-banner blurb, with a sensible fallback for unknown types. */
export const challengeTypeBlurb = (id: EChallengeType): string => CHALLENGE_TYPE_META[id]?.blurb ?? '';

/** `90` -> `1:30`, `45` -> `45s`, `0`/missing -> `—`. */
export function formatTime(seconds: number): string {
	if (!seconds || seconds <= 0) {
		return '—';
	}
	const minutes = Math.floor(seconds / 60);
	const secs = Math.round(seconds % 60);
	return minutes > 0 ? `${minutes}:${String(secs).padStart(2, '0')}` : `${secs}s`;
}

/** Thousands-separated for large counts, plain otherwise. */
export function formatCount(value: number): string {
	return value >= 1000 ? value.toLocaleString() : String(value);
}
