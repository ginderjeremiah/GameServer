import { describe, it, expect } from 'vitest';
import { formatNum, randomInt, capitalize, normalizeText, plural, keys, groupBy, enumPairs } from './functions';

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

describe('randomInt', () => {
	it('returns values within the range [num1, num2)', () => {
		for (let i = 0; i < 100; i++) {
			const val = randomInt(5, 10);
			expect(val).toBeGreaterThanOrEqual(5);
			expect(val).toBeLessThan(10);
		}
	});

	it('returns an integer', () => {
		for (let i = 0; i < 20; i++) {
			const val = randomInt(0, 100);
			expect(val).toBe(Math.floor(val));
		}
	});

	it('works with negative ranges', () => {
		for (let i = 0; i < 50; i++) {
			const val = randomInt(-10, -5);
			expect(val).toBeGreaterThanOrEqual(-10);
			expect(val).toBeLessThan(-5);
		}
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

	it('returns empty array for undefined', () => {
		expect(enumPairs(undefined)).toEqual([]);
	});
});
