import { describe, it, expect } from 'vitest';
import {
	passwordStrength,
	validateConfirm,
	validatePassword,
	validateUsername
} from '../../../routes/login/login-validation';

describe('validateUsername', () => {
	it('rejects an empty username as required', () => {
		expect(validateUsername('')).toEqual({ ok: false, msg: 'Required' });
	});

	it('rejects usernames shorter than 3 characters', () => {
		expect(validateUsername('ab')).toEqual({ ok: false, msg: 'At least 3 characters' });
	});

	it('rejects usernames longer than 20 characters', () => {
		expect(validateUsername('a'.repeat(21))).toEqual({ ok: false, msg: 'Max 20 characters' });
	});

	it('rejects usernames starting with a number', () => {
		expect(validateUsername('1abc')).toEqual({ ok: false, msg: "Can't start with a number" });
	});

	it('rejects usernames with disallowed characters', () => {
		expect(validateUsername('ab cd')).toEqual({ ok: false, msg: 'Letters, numbers, _ or - only' });
		expect(validateUsername('ab!cd')).toEqual({ ok: false, msg: 'Letters, numbers, _ or - only' });
	});

	it('accepts a valid username with letters, numbers, underscores and dashes', () => {
		expect(validateUsername('hero_99-x')).toEqual({ ok: true, msg: 'Looks good' });
	});

	it('accepts the boundary lengths of 3 and 20', () => {
		expect(validateUsername('abc').ok).toBe(true);
		expect(validateUsername('a' + '1'.repeat(19)).ok).toBe(true);
	});
});

describe('validatePassword', () => {
	it('rejects an empty password in either mode', () => {
		expect(validatePassword('', 'login')).toEqual({ ok: false, msg: 'Required' });
		expect(validatePassword('', 'signup')).toEqual({ ok: false, msg: 'Required' });
	});

	it('accepts any non-empty password when logging in', () => {
		expect(validatePassword('x', 'login')).toEqual({ ok: true, msg: '' });
	});

	it('enforces a minimum length when signing up', () => {
		expect(validatePassword('ab12', 'signup')).toEqual({ ok: false, msg: 'At least 8 characters' });
	});

	it('requires a mix of letters and numbers when signing up', () => {
		expect(validatePassword('abcdefgh', 'signup')).toEqual({
			ok: false,
			msg: 'Mix letters and numbers'
		});
		expect(validatePassword('12345678', 'signup')).toEqual({
			ok: false,
			msg: 'Mix letters and numbers'
		});
	});

	it('accepts a valid signup password', () => {
		expect(validatePassword('abcd1234', 'signup')).toEqual({ ok: true, msg: '' });
	});
});

describe('validateConfirm', () => {
	it('is always valid when logging in', () => {
		expect(validateConfirm('', 'anything', 'login')).toEqual({ ok: true, msg: '' });
	});

	it('requires a value when signing up', () => {
		expect(validateConfirm('', 'pw', 'signup')).toEqual({ ok: false, msg: 'Required' });
	});

	it('flags a mismatch with the password', () => {
		expect(validateConfirm('nope', 'pw', 'signup')).toEqual({
			ok: false,
			msg: "Passwords don't match"
		});
	});

	it('accepts a matching confirmation', () => {
		expect(validateConfirm('pw', 'pw', 'signup')).toEqual({ ok: true, msg: 'Match' });
	});
});

describe('passwordStrength', () => {
	it('scores an empty password as zero with a placeholder label', () => {
		expect(passwordStrength('')).toEqual({ score: 0, label: '—' });
	});

	it('scores a short weak password', () => {
		// 8+ chars only → 1 point.
		expect(passwordStrength('aaaaaaaa')).toEqual({ score: 1, label: 'Weak' });
	});

	it('caps the score at 4 filled segments while still labelling the highest tier', () => {
		// length>=8, length>=12, mixed case, digit, symbol → 5 points, capped to 4.
		expect(passwordStrength('Abcdefgh1234!')).toEqual({ score: 4, label: 'Strong' });
	});

	it('keeps the score and label in step for a mid-strength password', () => {
		// length>=8 (1) + digit (1) = 2 points.
		expect(passwordStrength('abcde123')).toEqual({ score: 2, label: 'Fair' });
	});
});
