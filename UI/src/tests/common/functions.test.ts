import { describe, it, expect, vi } from 'vitest';
import {
	formatNum,
	capitalize,
	normalizeText,
	plural,
	keys,
	groupBy,
	enumPairs,
	hasFlag,
	toggleFlag,
	delay
} from '../../lib/common/functions';

describe('formatNum', () => {
	it('formats integers without trailing zeros', () => {
		expect(formatNum(10)).toBe('10');
	});

	it('rounds to two decimal places', () => {
		expect(formatNum(1.005)).toBe('1');
		expect(formatNum(1.999)).toBe('2');
		expect(formatNum(3.456)).toBe('3.46');
	});

	it('strips trailing zeros from decimals', () => {
		expect(formatNum(1.1)).toBe('1.1');
		expect(formatNum(2.0)).toBe('2');
	});
});

describe('capitalize', () => {
	it('uppercases the first character', () => {
		expect(capitalize('hello')).toBe('Hello');
	});

	it('leaves already-capitalized strings unchanged', () => {
		expect(capitalize('Hello')).toBe('Hello');
	});

	it('handles single character', () => {
		expect(capitalize('a')).toBe('A');
	});

	it('returns an empty string for empty input rather than throwing', () => {
		expect(() => capitalize('')).not.toThrow();
		expect(capitalize('')).toBe('');
	});

	it('returns an empty string for undefined input rather than throwing', () => {
		expect(() => capitalize(undefined)).not.toThrow();
		expect(capitalize(undefined)).toBe('');
	});
});

describe('normalizeText', () => {
	it('capitalizes and inserts spaces before uppercase letters', () => {
		expect(normalizeText('camelCase')).toBe('Camel Case');
	});

	it('handles already-normalized text', () => {
		expect(normalizeText('Hello')).toBe('Hello');
	});

	it('splits multiple camelCase words', () => {
		expect(normalizeText('myLongVariableName')).toBe('My Long Variable Name');
	});

	it('returns an empty string for empty input rather than throwing', () => {
		expect(() => normalizeText('')).not.toThrow();
		expect(normalizeText('')).toBe('');
	});

	it('returns an empty string for undefined input rather than throwing', () => {
		// Hot display paths call `normalizeText(EAttribute[id])`, where an unknown id yields
		// `undefined`; this must degrade gracefully instead of crashing mid-render/tick (#493).
		expect(() => normalizeText(undefined)).not.toThrow();
		expect(normalizeText(undefined)).toBe('');
	});
});

describe('plural', () => {
	it('adds "ies" for words ending in y', () => {
		expect(plural('enemy')).toBe('enemies');
	});

	it('adds "es" for words ending in x', () => {
		expect(plural('box')).toBe('boxes');
	});

	it('adds "es" for words ending in s', () => {
		expect(plural('class')).toBe('classes');
	});

	it('adds "s" for other words', () => {
		expect(plural('item')).toBe('items');
	});
});

describe('keys', () => {
	it('returns typed keys of an object', () => {
		const obj = { a: 1, b: 2, c: 3 };
		expect(keys(obj)).toEqual(['a', 'b', 'c']);
	});

	it('returns empty array for undefined', () => {
		expect(keys(undefined)).toEqual([]);
	});
});

describe('groupBy', () => {
	it('groups items by the given function', () => {
		const items = [
			{ type: 'a', value: 1 },
			{ type: 'b', value: 2 },
			{ type: 'a', value: 3 }
		];
		const result = groupBy(items, (i) => i.type);
		expect(result['a']).toHaveLength(2);
		expect(result['b']).toHaveLength(1);
	});

	it('returns empty object for empty array', () => {
		expect(groupBy([], () => 'x')).toEqual({});
	});
});

describe('enumPairs', () => {
	it('converts a numeric enum to id/name pairs', () => {
		enum TestEnum {
			Foo,
			Bar,
			Baz
		}
		const pairs = enumPairs(TestEnum);
		expect(pairs).toEqual([
			{ id: 0, name: 'Foo' },
			{ id: 1, name: 'Bar' },
			{ id: 2, name: 'Baz' }
		]);
	});

	it('normalizes camelCase enum names', () => {
		enum TestEnum {
			MyValue = 0
		}
		const pairs = enumPairs(TestEnum);
		expect(pairs[0].name).toBe('My Value');
	});
});

describe('hasFlag', () => {
	// Bits mirror a [Flags] enum: Player=1, Item=2, Enemy=4.
	it('detects a set single-bit flag', () => {
		expect(hasFlag(1 | 4, 4)).toBe(true);
	});

	it('returns false for a clear flag', () => {
		expect(hasFlag(1 | 4, 2)).toBe(false);
	});

	it('treats None (0) as having no flags', () => {
		expect(hasFlag(0, 1)).toBe(false);
	});

	it('requires every bit of a multi-bit flag to be set', () => {
		expect(hasFlag(1 | 2, 2 | 4)).toBe(false);
		expect(hasFlag(2 | 4, 2 | 4)).toBe(true);
	});
});

describe('toggleFlag', () => {
	it('sets a flag without disturbing others', () => {
		expect(toggleFlag(1, 4, true)).toBe(1 | 4);
	});

	it('clears a flag without disturbing others', () => {
		expect(toggleFlag(1 | 2 | 4, 2, false)).toBe(1 | 4);
	});

	it('is a no-op when setting an already-set flag', () => {
		expect(toggleFlag(1 | 2, 2, true)).toBe(1 | 2);
	});

	it('is a no-op when clearing an already-clear flag', () => {
		expect(toggleFlag(1, 4, false)).toBe(1);
	});
});

describe('delay', () => {
	it('resolves only after the given number of milliseconds elapses', async () => {
		vi.useFakeTimers();
		try {
			const settled = vi.fn();
			const promise = delay(100).then(settled);

			// Not yet elapsed: the promise must still be pending.
			await vi.advanceTimersByTimeAsync(99);
			expect(settled).not.toHaveBeenCalled();

			// The final tick crosses the delay and resolves the promise.
			await vi.advanceTimersByTimeAsync(1);
			await promise;
			expect(settled).toHaveBeenCalledTimes(1);
		} finally {
			vi.useRealTimers();
		}
	});
});
