/* Relative "last played" formatting for the character-select cards. The select screen runs before
   the reference-data load, so zone names aren't available yet to show where a character left off —
   the last-activity timestamp is what distinguishes characters at a glance. Pure and time-injectable
   so it's unit-testable without freezing the clock. */

/**
 * Formats an ISO `lastActivity` timestamp as a coarse relative age (e.g. "3d ago"). Returns an empty
 * string for an unparseable value so the card can simply omit the line. Clamps a future timestamp to
 * "just now" rather than rendering a negative age.
 */
export function formatLastPlayed(iso: string, now: number = Date.now()): string {
	const then = Date.parse(iso);
	if (Number.isNaN(then)) {
		return '';
	}

	const seconds = Math.floor(Math.max(0, now - then) / 1000);
	if (seconds < 60) {
		return 'just now';
	}

	const minutes = Math.floor(seconds / 60);
	if (minutes < 60) {
		return `${minutes}m ago`;
	}

	const hours = Math.floor(minutes / 60);
	if (hours < 24) {
		return `${hours}h ago`;
	}

	const days = Math.floor(hours / 24);
	if (days < 30) {
		return `${days}d ago`;
	}

	const months = Math.floor(days / 30);
	if (months < 12) {
		return `${months}mo ago`;
	}

	return `${Math.floor(days / 365)}y ago`;
}
