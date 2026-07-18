import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

const postMock = vi.fn();
const constructorMock = vi.fn();

vi.mock('$lib/api/api-request', () => ({
	ApiRequest: class {
		constructor(endpoint: string) {
			constructorMock(endpoint);
		}
		post = postMock;
	}
}));

import { logout } from '$lib/api/logout';
import { getTokens, setTokens } from '$lib/api/token-store';

describe('logout', () => {
	let originalLocation: Location;

	beforeEach(() => {
		constructorMock.mockReset();
		postMock.mockReset().mockResolvedValue({ status: 200 });
		localStorage.clear();
		setTokens({ accessToken: 'access', refreshToken: 'refresh' });
		originalLocation = window.location;
		Object.defineProperty(window, 'location', {
			configurable: true,
			writable: true,
			value: { href: '', pathname: '/game' }
		});
	});

	afterEach(() => {
		Object.defineProperty(window, 'location', {
			configurable: true,
			writable: true,
			value: originalLocation
		});
		localStorage.clear();
	});

	it('revokes the refresh token, clears storage, and redirects to the login screen', async () => {
		await logout();

		expect(constructorMock).toHaveBeenCalledWith('Auth/Logout');
		expect(postMock).toHaveBeenCalledTimes(1);
		expect(postMock).toHaveBeenCalledWith({ refreshToken: 'refresh' });
		expect(getTokens()).toBeNull();
		expect(window.location.href).toBe('/');
	});

	it('still clears storage and redirects when there is no refresh token', async () => {
		localStorage.clear();

		await logout();

		expect(postMock).not.toHaveBeenCalled();
		expect(getTokens()).toBeNull();
		expect(window.location.href).toBe('/');
	});

	it('redirects only after the logout request resolves', async () => {
		let resolvePost: (value: unknown) => void = () => {};
		postMock.mockReturnValue(
			new Promise((resolve) => {
				resolvePost = resolve;
			})
		);

		const pending = logout();
		expect(window.location.href).toBe('');

		resolvePost({ status: 200 });
		await pending;

		expect(window.location.href).toBe('/');
	});
});
