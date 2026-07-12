import { describe, it, expect, beforeEach, vi } from 'vitest';
import {
	clearTokens,
	getAccessToken,
	getAccessTokenExpiry,
	getRefreshToken,
	getRoles,
	getTokens,
	setTokens
} from '$lib/api/token-store';

function makeToken(payload: Record<string, unknown>): string {
	// Mirror how the backend serializes a JWT payload: UTF-8 encode the JSON, base64-encode the raw
	// bytes (so multi-byte claims survive), then base64url-ify and strip the `=` padding.
	const bytes = new TextEncoder().encode(JSON.stringify(payload));
	const binary = Array.from(bytes, (b) => String.fromCharCode(b)).join('');
	const body = btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
	return `header.${body}.signature`;
}

function makeAccessToken(exp: number): string {
	return makeToken({ exp });
}

describe('token-store', () => {
	beforeEach(() => {
		localStorage.clear();
		// Reset the module's in-memory mirror/write-failure flag between tests too, since they're
		// module-level state that would otherwise leak across cases.
		clearTokens();
	});

	it('returns null when no tokens are stored', () => {
		expect(getTokens()).toBeNull();
		expect(getAccessToken()).toBeNull();
		expect(getRefreshToken()).toBeNull();
	});

	it('persists and reads back a token pair', () => {
		setTokens({ accessToken: 'a', refreshToken: 'r' });

		expect(getTokens()).toEqual({ accessToken: 'a', refreshToken: 'r' });
		expect(getAccessToken()).toBe('a');
		expect(getRefreshToken()).toBe('r');
	});

	it('replaces the stored pair on a subsequent set', () => {
		setTokens({ accessToken: 'a', refreshToken: 'r' });
		setTokens({ accessToken: 'a2', refreshToken: 'r2' });

		expect(getTokens()).toEqual({ accessToken: 'a2', refreshToken: 'r2' });
	});

	it('clears the stored pair', () => {
		setTokens({ accessToken: 'a', refreshToken: 'r' });
		clearTokens();

		expect(getTokens()).toBeNull();
	});

	it('treats a malformed stored entry as logged out', () => {
		localStorage.setItem('gameserver.auth-tokens', 'not-json');

		expect(getTokens()).toBeNull();
	});

	it('treats a partial stored entry as logged out', () => {
		localStorage.setItem('gameserver.auth-tokens', JSON.stringify({ accessToken: 'a' }));

		expect(getTokens()).toBeNull();
	});

	it('decodes the access token expiry claim', () => {
		const exp = Math.floor(Date.now() / 1000) + 1000;
		setTokens({ accessToken: makeAccessToken(exp), refreshToken: 'r' });

		expect(getAccessTokenExpiry()).toBe(exp);
	});

	it('returns null expiry for a non-JWT access token', () => {
		setTokens({ accessToken: 'opaque', refreshToken: 'r' });

		expect(getAccessTokenExpiry()).toBeNull();
	});

	it('returns null expiry when not logged in', () => {
		expect(getAccessTokenExpiry()).toBeNull();
	});

	it('returns an empty role list when not logged in', () => {
		expect(getRoles()).toEqual([]);
	});

	it('returns an empty role list for a non-JWT access token', () => {
		setTokens({ accessToken: 'opaque', refreshToken: 'r' });

		expect(getRoles()).toEqual([]);
	});

	it('returns an empty role list when the token carries no role claim', () => {
		setTokens({ accessToken: makeToken({ sub: '1' }), refreshToken: 'r' });

		expect(getRoles()).toEqual([]);
	});

	it('decodes a single string role claim into a one-element array', () => {
		setTokens({ accessToken: makeToken({ role: 'Admin' }), refreshToken: 'r' });

		expect(getRoles()).toEqual(['Admin']);
	});

	it('decodes an array role claim', () => {
		setTokens({ accessToken: makeToken({ role: ['Admin', 'Moderator'] }), refreshToken: 'r' });

		expect(getRoles()).toEqual(['Admin', 'Moderator']);
	});

	it('ignores non-string entries in an array role claim', () => {
		setTokens({ accessToken: makeToken({ role: ['Admin', 5, null] }), refreshToken: 'r' });

		expect(getRoles()).toEqual(['Admin']);
	});

	it('decodes a payload whose base64url length is not a multiple of 4 (needs re-padding)', () => {
		// `makeToken` strips `=` padding, so pick a payload whose base64url length isn't divisible by
		// 4 to exercise the re-pad path before `atob`.
		const token = makeToken({ role: 'Admin', sub: 'abc' });
		const body = token.split('.')[1];
		expect(body.length % 4).not.toBe(0);

		setTokens({ accessToken: token, refreshToken: 'r' });

		expect(getRoles()).toEqual(['Admin']);
	});

	it('round-trips a role claim containing multi-byte UTF-8 (accented + emoji)', () => {
		const role = 'Admiñ-🛡';
		setTokens({ accessToken: makeToken({ role }), refreshToken: 'r' });

		expect(getRoles()).toEqual([role]);
	});

	it('decodes an expiry claim alongside a non-ASCII username claim', () => {
		const exp = Math.floor(Date.now() / 1000) + 1000;
		setTokens({ accessToken: makeToken({ exp, name: 'José 🎮' }), refreshToken: 'r' });

		expect(getAccessTokenExpiry()).toBe(exp);
	});

	it('fails closed on a malformed (non-base64) payload segment', () => {
		// A third segment is present so the split passes, but the payload isn't valid base64/JSON.
		setTokens({ accessToken: 'header.@@not-base64@@.signature', refreshToken: 'r' });

		expect(getAccessTokenExpiry()).toBeNull();
		expect(getRoles()).toEqual([]);
	});

	describe('degraded storage', () => {
		it('serves the freshly set pair from memory when the storage write throws (quota exceeded)', () => {
			const spy = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
				throw new DOMException('quota exceeded', 'QuotaExceededError');
			});

			expect(() => setTokens({ accessToken: 'a', refreshToken: 'r' })).not.toThrow();
			expect(getTokens()).toEqual({ accessToken: 'a', refreshToken: 'r' });
			// The write genuinely never landed in storage.
			expect(localStorage.getItem('gameserver.auth-tokens')).toBeNull();

			spy.mockRestore();
		});

		it('resumes reading from storage once a write succeeds again after a prior failure', () => {
			const spy = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
				throw new DOMException('quota exceeded', 'QuotaExceededError');
			});
			setTokens({ accessToken: 'a', refreshToken: 'r' });
			spy.mockRestore();

			setTokens({ accessToken: 'a2', refreshToken: 'r2' });

			expect(getTokens()).toEqual({ accessToken: 'a2', refreshToken: 'r2' });
			expect(localStorage.getItem('gameserver.auth-tokens')).toBe(
				JSON.stringify({ accessToken: 'a2', refreshToken: 'r2' })
			);
		});

		it('falls back to memory when reading storage throws', () => {
			setTokens({ accessToken: 'a', refreshToken: 'r' });

			const spy = vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
				throw new DOMException('storage blocked', 'SecurityError');
			});

			expect(getTokens()).toEqual({ accessToken: 'a', refreshToken: 'r' });

			spy.mockRestore();
		});

		it('clearTokens resets the memory mirror and the write-failure state', () => {
			const spy = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
				throw new DOMException('quota exceeded', 'QuotaExceededError');
			});
			setTokens({ accessToken: 'a', refreshToken: 'r' });
			spy.mockRestore();

			clearTokens();

			expect(getTokens()).toBeNull();
		});
	});
});
