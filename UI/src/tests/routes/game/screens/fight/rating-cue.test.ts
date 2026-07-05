import { describe, it, expect } from 'vitest';
import { ratingCue, ratingCueColorVar, ratingCueLabel } from '$routes/game/screens/fight/rating-cue';

describe('ratingCue', () => {
	it('reads as matched when the player rating is not yet meaningful (zero or negative)', () => {
		expect(ratingCue(100, 0)).toBe('matched');
		expect(ratingCue(100, -5)).toBe('matched');
	});

	it('is dangerous at and above a matched fight (ratio >= 1)', () => {
		expect(ratingCue(100, 100)).toBe('dangerous'); // ratio exactly 1
		expect(ratingCue(250, 100)).toBe('dangerous'); // well above
	});

	it('is matched just below the dangerous boundary, down to the matched/manageable boundary', () => {
		expect(ratingCue(99, 100)).toBe('matched'); // just under ratio 1
		expect(ratingCue(60, 100)).toBe('matched'); // ratio exactly 0.6
	});

	it('is manageable just below the matched boundary, down to the manageable/trivial boundary', () => {
		expect(ratingCue(59, 100)).toBe('manageable'); // just under ratio 0.6
		expect(ratingCue(30, 100)).toBe('manageable'); // ratio exactly 0.3
	});

	it('is trivial below the manageable boundary', () => {
		expect(ratingCue(29, 100)).toBe('trivial'); // just under ratio 0.3
		expect(ratingCue(1, 100)).toBe('trivial'); // well below
	});
});

describe('ratingCueLabel', () => {
	it('gives each cue a display label', () => {
		expect(ratingCueLabel('trivial')).toBe('Trivial');
		expect(ratingCueLabel('manageable')).toBe('Manageable');
		expect(ratingCueLabel('matched')).toBe('Matched');
		expect(ratingCueLabel('dangerous')).toBe('Dangerous');
	});
});

describe('ratingCueColorVar', () => {
	it('maps each cue to an existing semantic CSS variable, never a new hue', () => {
		expect(ratingCueColorVar('trivial')).toBe('--text-muted');
		expect(ratingCueColorVar('manageable')).toBe('--success');
		expect(ratingCueColorVar('matched')).toBe('--warning');
		expect(ratingCueColorVar('dangerous')).toBe('--enemy-accent');
	});
});
