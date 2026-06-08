import { describe, it, expect } from 'vitest';
import {
	coversTick,
	critGap,
	drawInterval,
	freeStart,
	handCap,
	isAttackKind,
	resolveStrike,
	shuffle,
	spanActiveAt,
	type Span
} from '$lib/card-game';

describe('loom-core — attribute formulas', () => {
	describe('handCap = 4 + ⌊log₁₀(DEX)⌋', () => {
		it('is 4 at low DEX and scales by powers of ten', () => {
			expect(handCap(8)).toBe(4);
			expect(handCap(50)).toBe(5);
			expect(handCap(200)).toBe(6);
			expect(handCap(1000)).toBe(7);
		});
		it('clamps DEX to at least 1 (never below 4)', () => {
			expect(handCap(1)).toBe(4);
			expect(handCap(0)).toBe(4);
			expect(handCap(-10)).toBe(4);
		});
	});

	describe('critGap = max(6, 26 − ⌊LUCK⌋)', () => {
		it('tightens with Luck', () => {
			expect(critGap(0)).toBe(26);
			expect(critGap(14)).toBe(12);
		});
		it('floors at 6', () => {
			expect(critGap(20)).toBe(6);
			expect(critGap(100)).toBe(6);
		});
	});

	describe('drawInterval = max(1.0, 2.0 − 0.005·AGI)', () => {
		it('starts at 2s and quickens with Agility', () => {
			expect(drawInterval(0)).toBeCloseTo(2.0);
			expect(drawInterval(10)).toBeCloseTo(1.95);
			expect(drawInterval(200)).toBeCloseTo(1.0);
		});
		it('floors at 1s', () => {
			expect(drawInterval(300)).toBe(1.0);
			expect(drawInterval(1000)).toBe(1.0);
		});
	});
});

describe('loom-core — half-open coverage', () => {
	const span: Span = { start: 9, end: 16 };

	it('covers the start and interior but NOT the end (so impacts land centered)', () => {
		expect(coversTick(span, 9)).toBe(true);
		expect(coversTick(span, 15)).toBe(true);
		expect(coversTick(span, 16)).toBe(false);
		expect(coversTick(span, 8)).toBe(false);
	});

	it('lets adjacent queued spans tile with no double-covered seam', () => {
		const a: Span = { start: 9, end: 16 };
		const b: Span = { start: 16, end: 23 };
		// the seam tick (16) is covered by exactly one span
		expect(coversTick(a, 16)).toBe(false);
		expect(coversTick(b, 16)).toBe(true);
	});

	it('spanActiveAt reports whether any lane span covers a tick', () => {
		const lane: Span[] = [
			{ start: 2, end: 5 },
			{ start: 9, end: 16 }
		];
		expect(spanActiveAt(lane, 3)).toBe(true);
		expect(spanActiveAt(lane, 7)).toBe(false);
		expect(spanActiveAt(lane, 16)).toBe(false);
	});
});

describe('loom-core — freeStart (no-overlap queue placement)', () => {
	it('returns the desired start on an empty lane (never in the past)', () => {
		expect(freeStart([], 5, 7, 0)).toBe(5);
		expect(freeStart([], 5, 7, 8.2)).toBe(9); // clamped to ceil(playTick)
	});

	it('chains same-type blocks tail-to-tail', () => {
		const lane: Span[] = [];
		const first = freeStart(lane, 2, 7, 0);
		lane.push({ start: first, end: first + 7 });
		const second = freeStart(lane, 2, 7, 0);
		expect(first).toBe(2);
		expect(second).toBe(9); // [2,9] then [9,16]
	});

	it('snaps three spans aimed at the same tick into a seamless wall', () => {
		const lane: Span[] = [];
		const starts: number[] = [];
		for (let i = 0; i < 3; i++) {
			const s = freeStart(lane, 7, 7, 0);
			starts.push(s);
			lane.push({ start: s, end: s + 7 });
		}
		expect(starts).toEqual([7, 14, 21]); // [7,14][14,21][21,28]
	});

	it('chains shorter spans on their own lane', () => {
		const lane: Span[] = [];
		const a = freeStart(lane, 2, 3, 0);
		lane.push({ start: a, end: a + 3 });
		const b = freeStart(lane, 2, 3, 0);
		expect([a, b]).toEqual([2, 5]); // [2,5][5,8]
	});

	it('aims into a gap without disturbing existing spans', () => {
		const lane: Span[] = [{ start: 10, end: 13 }];
		// a 3-tick span aimed at 4 fits cleanly before the existing one
		expect(freeStart(lane, 4, 3, 0)).toBe(4);
	});
});

describe('loom-core — resolveStrike', () => {
	it('deals base damage with no crit or guard', () => {
		expect(resolveStrike(16, 17, [], [])).toEqual({ dmg: 16, crit: false, guarded: false });
	});

	it('doubles on an unused crit mark at the resolve tick', () => {
		const out = resolveStrike(16, 17, [{ tick: 17, used: false }], []);
		expect(out).toEqual({ dmg: 32, crit: true, guarded: false });
	});

	it('ignores a crit mark already used', () => {
		const out = resolveStrike(16, 17, [{ tick: 17, used: true }], []);
		expect(out.crit).toBe(false);
		expect(out.dmg).toBe(16);
	});

	it('reduces a guarded strike to 40% (floored)', () => {
		const out = resolveStrike(16, 17, [], [{ start: 16, end: 20 }]);
		expect(out).toEqual({ dmg: 6, crit: false, guarded: true }); // ⌊16·0.4⌋ = 6
	});

	it('applies crit before guard', () => {
		const out = resolveStrike(16, 17, [{ tick: 17, used: false }], [{ start: 16, end: 20 }]);
		expect(out).toEqual({ dmg: 12, crit: true, guarded: true }); // ⌊32·0.4⌋ = 12
	});

	it('does not guard a strike landing on the (exclusive) guard end tick', () => {
		const out = resolveStrike(16, 20, [], [{ start: 16, end: 20 }]);
		expect(out.guarded).toBe(false);
	});
});

describe('loom-core — misc', () => {
	it('isAttackKind separates strike/channel from block', () => {
		expect(isAttackKind('attack')).toBe(true);
		expect(isAttackKind('channel')).toBe(true);
		expect(isAttackKind('block')).toBe(false);
	});

	it('shuffle is a deterministic permutation under a seeded rng', () => {
		const seq = [0.1, 0.9, 0.5, 0.3, 0.7, 0.2];
		const makeRng = () => {
			let i = 0;
			return () => seq[i++ % seq.length];
		};
		const original = [1, 2, 3, 4, 5, 6];
		const a = shuffle([...original], makeRng());
		const b = shuffle([...original], makeRng());
		// nothing lost (a permutation) and identical for the same seed sequence
		expect([...a].sort((x, y) => x - y)).toEqual(original);
		expect(a).toEqual(b);
	});
});
