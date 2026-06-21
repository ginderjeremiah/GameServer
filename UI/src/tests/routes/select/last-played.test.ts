import { describe, it, expect } from 'vitest';
import { formatLastPlayed } from '$routes/select/last-played';

const now = Date.parse('2026-06-21T12:00:00Z');

describe('formatLastPlayed', () => {
	it('renders coarse relative ages', () => {
		expect(formatLastPlayed('2026-06-21T11:59:30Z', now)).toBe('just now');
		expect(formatLastPlayed('2026-06-21T11:30:00Z', now)).toBe('30m ago');
		expect(formatLastPlayed('2026-06-21T09:00:00Z', now)).toBe('3h ago');
		expect(formatLastPlayed('2026-06-18T12:00:00Z', now)).toBe('3d ago');
		expect(formatLastPlayed('2026-05-01T12:00:00Z', now)).toBe('1mo ago');
		expect(formatLastPlayed('2024-06-21T12:00:00Z', now)).toBe('2y ago');
	});

	it('clamps a future timestamp to "just now" rather than a negative age', () => {
		expect(formatLastPlayed('2026-06-21T13:00:00Z', now)).toBe('just now');
	});

	it('returns an empty string for an unparseable timestamp', () => {
		expect(formatLastPlayed('not-a-date', now)).toBe('');
		expect(formatLastPlayed('', now)).toBe('');
	});
});
