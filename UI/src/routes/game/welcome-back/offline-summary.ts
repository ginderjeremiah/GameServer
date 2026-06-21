/* Pure projections for the welcome-back gate (#1043). Kept DOM-free and dependency-injected so the
   formatting and challenge-unlock resolution are unit-testable without rendering — the same pure-logic
   split the other screens use. The gate component wires these to the reactive `staticData` store. */

import type { IChallenge, IChallengeCompletedModel } from '$lib/api';
import { resolveUnlockReward, type RewardRefs, type UnlockReward } from '$lib/common';

/** A completed challenge plus the reward it unlocked, resolved for the gate's "what you unlocked" list. */
export interface CompletedChallengeView {
	challengeId: number;
	name: string;
	/** The unlocked item/mod/skill, or null for a challenge that grants no direct reward. */
	reward: UnlockReward | null;
}

/** The reference pools the gate resolves challenge names + rewards against. */
export interface ChallengeRefs extends RewardRefs {
	challenges?: (IChallenge | undefined)[];
}

/**
 * Human-readable away duration from milliseconds, showing the two most significant units (e.g. `3h 12m`,
 * `2d 4h`, `1h 0m`), with a single-unit `Xm` floor under an hour. The window is floored to whole minutes —
 * sub-minute away times never reach the gate (the backend's 5-minute floor), so seconds are not surfaced.
 */
export function formatAwayDuration(awayMs: number): string {
	const totalMinutes = Math.max(0, Math.floor(awayMs / 60_000));
	const days = Math.floor(totalMinutes / 1440);
	const hours = Math.floor((totalMinutes % 1440) / 60);
	const minutes = totalMinutes % 60;

	if (days > 0) {
		return `${days}d ${hours}h`;
	}
	if (hours > 0) {
		return `${hours}h ${minutes}m`;
	}
	return `${minutes}m`;
}

/**
 * Resolves each offline-completed challenge to its display name and unlocked reward from the reference
 * data, reusing the shared {@link resolveUnlockReward} so the reward naming/accent matches every other
 * surface. A challenge missing from the catalogue still renders with a safe fallback name.
 */
export function resolveCompletedChallenges(
	completed: IChallengeCompletedModel[],
	refs: ChallengeRefs
): CompletedChallengeView[] {
	return completed.map((c) => {
		const challenge = refs.challenges?.[c.challengeId];
		return {
			challengeId: c.challengeId,
			name: challenge?.name ?? 'Unknown challenge',
			reward: challenge ? resolveUnlockReward(challenge, refs) : null
		};
	});
}
