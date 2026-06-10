import { describe, it, expect } from 'vitest';
import { firstFree } from '$routes/admin/workbench/entities/helpers';
import type { SelectOption } from '$routes/admin/workbench/entities/types';

const opts: SelectOption[] = [
	{ value: 0, text: 'Strength' },
	{ value: 1, text: 'Agility' },
	{ value: 2, text: 'Luck' }
];

describe('firstFree', () => {
	it('returns the first option not already taken', () => {
		expect(firstFree([0], opts)).toBe(1);
		expect(firstFree([0, 1], opts)).toBe(2);
	});

	it('returns the first option when nothing is taken', () => {
		expect(firstFree([], opts)).toBe(0);
	});

	it('falls back to the first option when every option is taken', () => {
		expect(firstFree([0, 1, 2], opts)).toBe(0);
	});
});
