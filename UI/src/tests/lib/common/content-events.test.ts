import { describe, it, expect } from 'vitest';
import {
	newlyTrue,
	newlyInSet,
	crossedThreshold,
	isFirstCrit,
	isFirstDodge,
	isFirstCooldownRecharge
} from '$lib/common';

describe('newlyTrue', () => {
	it('is true only on a false-to-true flip', () => {
		expect(newlyTrue(false, true)).toBe(true);
	});

	it('is false when already true before', () => {
		expect(newlyTrue(true, true)).toBe(false);
	});

	it('is false when still false', () => {
		expect(newlyTrue(false, false)).toBe(false);
	});

	it('is false on a true-to-false flip', () => {
		expect(newlyTrue(true, false)).toBe(false);
	});
});

describe('newlyInSet', () => {
	it('collects ids present in after but absent from before', () => {
		expect(newlyInSet(new Set([1, 2]), new Set([1, 2, 3]))).toEqual([3]);
	});

	it('is empty when nothing new was added', () => {
		expect(newlyInSet(new Set([1, 2]), new Set([1, 2]))).toEqual([]);
	});

	it('does not report an id that disappeared', () => {
		expect(newlyInSet(new Set([1, 2]), new Set([1]))).toEqual([]);
	});
});

describe('crossedThreshold', () => {
	it('is true when before is under the threshold and after meets it', () => {
		expect(crossedThreshold(4, 5, 5)).toBe(true);
	});

	it('is false when before already met the threshold', () => {
		expect(crossedThreshold(5, 6, 5)).toBe(false);
	});

	it('is false when after still falls short', () => {
		expect(crossedThreshold(3, 4, 5)).toBe(false);
	});
});

const activation = (over: Partial<{ byPlayer: boolean; crit: boolean; dodged: boolean; counter: boolean }>) => ({
	byPlayer: false,
	crit: false,
	dodged: false,
	counter: false,
	...over
});

describe('isFirstCrit', () => {
	it('fires the first time a player activation lands a crit', () => {
		expect(isFirstCrit(false, [activation({ byPlayer: true, crit: true })])).toBe(true);
	});

	it('does not fire again once a crit has already landed', () => {
		expect(isFirstCrit(true, [activation({ byPlayer: true, crit: true })])).toBe(false);
	});

	it('ignores a crit flag on a non-player activation', () => {
		expect(isFirstCrit(false, [activation({ byPlayer: false, crit: true })])).toBe(false);
	});

	it('does not fire when no activation crit', () => {
		expect(isFirstCrit(false, [activation({ byPlayer: true, crit: false })])).toBe(false);
	});
});

describe('isFirstDodge', () => {
	it('fires the first time an activation is dodged', () => {
		expect(isFirstDodge(false, [activation({ dodged: true })])).toBe(true);
	});

	it('does not fire again once a dodge has already occurred', () => {
		expect(isFirstDodge(true, [activation({ dodged: true })])).toBe(false);
	});

	it('does not fire when nothing was dodged', () => {
		expect(isFirstDodge(false, [activation({ dodged: false })])).toBe(false);
	});
});

describe('isFirstCooldownRecharge', () => {
	it('fires the first time a non-counter player activation fires', () => {
		expect(isFirstCooldownRecharge(false, [activation({ byPlayer: true, counter: false })])).toBe(true);
	});

	it('does not fire again once a skill has already recharged and fired', () => {
		expect(isFirstCooldownRecharge(true, [activation({ byPlayer: true, counter: false })])).toBe(false);
	});

	it('ignores the riposte counter fire', () => {
		expect(isFirstCooldownRecharge(false, [activation({ byPlayer: true, counter: true })])).toBe(false);
	});

	it('ignores enemy activations', () => {
		expect(isFirstCooldownRecharge(false, [activation({ byPlayer: false })])).toBe(false);
	});
});
