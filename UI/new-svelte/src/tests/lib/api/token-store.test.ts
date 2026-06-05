import { describe, it, expect, beforeEach } from 'vitest';
import {
	clearTokens,
	getAccessToken,
	getAccessTokenExpiry,
	getRefreshToken,
	getTokens,
	setTokens
} from '$lib/api/token-store';

function makeAccessToken(exp: number): string {
	const payload = btoa(JSON.stringify({ exp })).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
	return `header.${payload}.signature`;
}

describe('token-store', () => {
	beforeEach(() => {
		localStorage.clear();
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
});
