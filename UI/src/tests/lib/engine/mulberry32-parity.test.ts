import { describe, it, expect } from 'vitest';
import { Mulberry32 } from '$lib/engine/mulberry32';

/**
 * Cross-implementation parity vector for the seeded battle RNG primitive.
 * Every seed/output row here MUST mirror — with identical seeds and identical
 * expected draws — a row in the backend suite
 * Game.Core.Tests/Battle/Mulberry32ParityTests.cs (the `Vectors` table).
 *
 * The two ports (mulberry32.ts here, Game.Core/Battle/Mulberry32.cs on the server)
 * reach a bit-identical 32-bit stream through subtly different language semantics —
 * JS `Math.imul` and `>>> 0` unsigned coercion versus C# native `uint` wrap-around
 * and logical `>>`. The frontend/backend battle replay (anti-cheat) depends on them
 * agreeing once seeded crit/dodge effects (#178) start consuming `next()`, so this
 * vector locks the two ports together before anything depends on them.
 */

// 2^32 — the divisor the implementation uses to map a 32-bit draw into [0, 1).
// Expected outputs are written as `numerator / UINT32_RANGE`: because the divisor is
// a power of two and every numerator is a 32-bit unsigned integer, the quotient is an
// exact double, so both ports produce bit-identical values.
const UINT32_RANGE = 4294967296;

// The shared parity matrix: a fixed seed and the exact 32-bit numerators of the first
// six `next()` draws it must produce. Seeds span the edge cases (zero, one) and high-bit
// values (a large arbitrary seed and one near uint max) where the cross-language
// unsigned-shift/wrap semantics are most likely to diverge.
const vectors: { name: string; seed: number; numerators: number[] }[] = [
	{ name: 'seedZero', seed: 0, numerators: [1144304738, 1416247, 958946056, 627933444, 2007157716, 2340967985] },
	{ name: 'seedOne', seed: 1, numerators: [2693262067, 11749833, 2265367787, 4213581821, 4159151403, 1207330352] },
	{
		name: 'seedArbitrary',
		seed: 123456789,
		numerators: [1107202814, 4169434471, 3372958138, 885470128, 1301683845, 3208624240]
	},
	{
		name: 'seedHighBits',
		seed: 0xdeadbeef,
		numerators: [4043151706, 1147597007, 3315858022, 1538288752, 2042435954, 3600176436]
	}
];

describe('Mulberry32 parity with backend', () => {
	for (const vector of vectors) {
		it(`matches the backend for the ${vector.name} vector`, () => {
			const rng = new Mulberry32(vector.seed);

			for (const numerator of vector.numerators) {
				expect(rng.next()).toBe(numerator / UINT32_RANGE);
			}
		});
	}
});
