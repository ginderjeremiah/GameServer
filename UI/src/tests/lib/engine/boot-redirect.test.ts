import { describe, it, expect } from 'vitest';
import { bootRedirect, shouldReturnToLogin } from '$lib/engine/boot-redirect';

describe('bootRedirect', () => {
	it('keeps a fully-restored session on the in-app route it loaded on', () => {
		// The /admin refresh fix: a "game" resume stays put rather than bouncing to /game.
		expect(bootRedirect('game', '/admin')).toBeNull();
		expect(bootRedirect('game', '/game')).toBeNull();
	});

	it('hands a "game" resume off to /game from the transient boot/auth routes', () => {
		expect(bootRedirect('game', '/')).toBe('game');
		expect(bootRedirect('game', '/select')).toBe('game');
		expect(bootRedirect('game', '/loading')).toBe('game');
	});

	it('always routes a "loading" resume to the loading screen', () => {
		expect(bootRedirect('loading', '/')).toBe('loading');
		expect(bootRedirect('loading', '/admin')).toBe('loading');
		expect(bootRedirect('loading', '/game')).toBe('loading');
	});

	it('sends a failed restore back to login from a protected route, staying put on /', () => {
		expect(bootRedirect('login', '/admin')).toBe('login');
		expect(bootRedirect('login', '/game')).toBe('login');
		expect(bootRedirect('login', '/select')).toBe('login');
		expect(bootRedirect('login', '/loading')).toBe('login');
		expect(bootRedirect('login', '/')).toBeNull();
	});
});

describe('shouldReturnToLogin', () => {
	it('does not bounce the pre-player auth routes when no player is loaded', () => {
		// The select screen is reached after login but before a character is selected/loaded, so the
		// post-boot safety net must not redirect it back to login for a missing player (the bug that
		// broke the whole login flow).
		expect(shouldReturnToLogin(false, '/')).toBe(false);
		expect(shouldReturnToLogin(false, '/select')).toBe(false);
	});

	it('returns to login when a loaded player is lost on a protected route', () => {
		expect(shouldReturnToLogin(false, '/game')).toBe(true);
		expect(shouldReturnToLogin(false, '/admin')).toBe(true);
		expect(shouldReturnToLogin(false, '/loading')).toBe(true);
	});

	it('stays put on any route while a player is loaded', () => {
		expect(shouldReturnToLogin(true, '/game')).toBe(false);
		expect(shouldReturnToLogin(true, '/select')).toBe(false);
	});
});
