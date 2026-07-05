/* Categorizes an enemy's combat rating against the player's own (spike #1526 Decision 7) so the fight
   screen can make the anti-grind curve legible ("you've outgrown this zone") instead of a bare number.
   Purely a display categorization — it mirrors where the reward curve saturates/falls off
   (`XP/kill = k * enemyRating * min(enemyRating / playerRating, 1)^2`, see game-design.md's Experience
   Rewards) but never feeds battle math; both ratings are server-computed and shown as-is. */

export type RatingCue = 'trivial' | 'manageable' | 'matched' | 'dangerous';

const CUE_LABEL: Record<RatingCue, string> = {
	trivial: 'Trivial',
	manageable: 'Manageable',
	matched: 'Matched',
	dangerous: 'Dangerous'
};

// Reuses existing semantic tokens rather than introducing new hues (docs/frontend.md's styling
// convention): muted for a fight not worth the player's time, success for a comfortable win, warning
// for an even matchup, and the enemy accent once the enemy is at or above the player's own rating.
const CUE_COLOR_VAR: Record<RatingCue, string> = {
	trivial: '--text-muted',
	manageable: '--success',
	matched: '--warning',
	dangerous: '--enemy-accent'
};

/** The ratio bands a matchup falls into, relative to the reward curve's matched-fight saturation point
 *  (ratio 1 = enemyRating equals playerRating). */
export function ratingCue(enemyRating: number, playerRating: number): RatingCue {
	if (playerRating <= 0) {
		// No meaningful comparison yet (e.g. the player rating hasn't loaded) — read as an even matchup
		// rather than implying a spurious extreme.
		return 'matched';
	}
	const ratio = enemyRating / playerRating;
	if (ratio >= 1) {
		return 'dangerous';
	}
	if (ratio >= 0.6) {
		return 'matched';
	}
	if (ratio >= 0.3) {
		return 'manageable';
	}
	return 'trivial';
}

export function ratingCueLabel(cue: RatingCue): string {
	return CUE_LABEL[cue];
}

export function ratingCueColorVar(cue: RatingCue): string {
	return CUE_COLOR_VAR[cue];
}
