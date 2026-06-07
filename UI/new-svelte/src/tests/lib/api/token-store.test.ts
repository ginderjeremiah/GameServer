import { describe, it, expect, beforeEach } from 'vitest';
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
	const body = btoa(JSON.stringify(payload)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
	return `header.${body}.signature`;
}

function makeAccessToken(exp: number): string {
	return makeToken({ exp });
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
});
