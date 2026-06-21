import { describe, it, expect } from 'vitest';
import { validatePlayerName, MAX_NAME } from '$routes/select/player-name';

describe('validatePlayerName', () => {
	it('accepts a normal name and returns the trimmed value to submit', () => {
		const result = validatePlayerName('  Hero  ');
		expect(result.ok).toBe(true);
		expect(result.name).toBe('Hero');
		expect(result.msg).toBe('');
	});

	it('accepts internal spaces and the maximum length', () => {
		expect(validatePlayerName('Sir Lancelot').ok).toBe(true);
		expect(validatePlayerName('12345678901234567890').ok).toBe(true); // exactly 20
	});

	it('rejects a blank or whitespace-only name', () => {
		for (const blank of ['', '   ', '\t\n']) {
			const result = validatePlayerName(blank);
			expect(result.ok).toBe(false);
			expect(result.name).toBe('');
		}
	});

	it('rejects a name longer than the max after trimming', () => {
		const result = validatePlayerName('123456789012345678901'); // 21 chars
		expect(result.ok).toBe(false);
		expect(result.msg).toContain(String(MAX_NAME));
	});

	it('rejects names containing control characters', () => {
		expect(validatePlayerName('Bad\tName').ok).toBe(false); // tab (C0)
		expect(validatePlayerName('Bad\nName').ok).toBe(false); // newline (C0)
		expect(validatePlayerName('Bad\u0000Name').ok).toBe(false); // null (C0)
		expect(validatePlayerName('Bad\u007fName').ok).toBe(false); // DEL (C1 range start)
	});
});
