import { describe, it, expect } from 'vitest';
import { Mulberry32 } from '$lib/engine/mulberry32';

/**
 * Behavioural unit tests for the frontend Mulberry32 RNG primitive.
 * The cross-implementation contract (the exact output sequence the backend port
 * must also produce) lives in mulberry32-parity.test.ts; this suite only covers
 * the language-agnostic properties (determinism, range, seed sensitivity).
 */
describe('Mulberry32', () => {
	it('produces an identical sequence for the same seed', () => {
		const a = new Mulberry32(0x12345678);
		const b = new Mulberry32(0x12345678);

		for (let i = 0; i < 100; i++) {
			expect(a.next()).toBe(b.next());
		}
	});

	it('diverges immediately for different seeds', () => {
		const a = new Mulberry32(1);
		const b = new Mulberry32(2);

		expect(a.next()).not.toBe(b.next());
	});

	it.each([0, 1, 123456789, 0xdeadbeef, 0xffffffff])('only returns values in [0, 1) for seed %i', (seed) => {
		const rng = new Mulberry32(seed);

		for (let i = 0; i < 1000; i++) {
			const value = rng.next();
			expect(value).toBeGreaterThanOrEqual(0);
			expect(value).toBeLessThan(1);
		}
	});

	it('advances the stream so successive draws differ', () => {
		const rng = new Mulberry32(42);

		expect(rng.next()).not.toBe(rng.next());
	});
});
