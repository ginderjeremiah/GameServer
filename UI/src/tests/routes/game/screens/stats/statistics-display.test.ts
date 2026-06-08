import { describe, it, expect } from 'vitest';
import { fmtCount, fmtDamage, fmtTime, fmtValue } from '$routes/game/screens/stats/statistics-display';

describe('fmtTime', () => {
	it('shows sub-minute durations in seconds (1 dp under 10s, whole otherwise)', () => {
		expect(fmtTime(1.8)).toBe('1.8s');
		expect(fmtTime(9.4)).toBe('9.4s');
		expect(fmtTime(12)).toBe('12s');
		expect(fmtTime(45)).toBe('45s');
	});

	it('shows minute durations as `Xm SSs` with zero-padded seconds', () => {
		expect(fmtTime(90)).toBe('1m 30s');
		expect(fmtTime(125)).toBe('2m 05s');
	});

	it('shows hour durations as `Xh MMm` with zero-padded minutes', () => {
		expect(fmtTime(3725)).toBe('1h 02m');
		expect(fmtTime(12420)).toBe('3h 27m');
	});
});

describe('fmtCount / fmtDamage', () => {
	it('render rounded, thousands-separated integers', () => {
		expect(fmtCount(1234)).toBe('1,234');
		expect(fmtDamage(186400)).toBe('186,400');
	});
});

describe('fmtValue', () => {
	it('dispatches to the formatter for the statistic’s unit', () => {
		expect(fmtValue(120, 'count')).toBe('120');
		expect(fmtValue(1820, 'damage')).toBe('1,820');
		expect(fmtValue(125, 'time')).toBe('2m 05s');
	});
});
