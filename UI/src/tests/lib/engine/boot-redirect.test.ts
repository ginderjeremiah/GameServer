import { describe, it, expect } from 'vitest';
import { bootRedirect } from '$lib/engine/boot-redirect';

describe('bootRedirect', () => {
	it('keeps a fully-restored session on the in-app route it loaded on', () => {
		// The /admin refresh fix: a "game" resume stays put rather than bouncing to /game.
		expect(bootRedirect('game', '/admin')).toBeNull();
		expect(bootRedirect('game', '/game')).toBeNull();
	});

	it('hands a "game" resume off to /game from the transient boot/auth routes', () => {
		expect(bootRedirect('game', '/')).toBe('game');
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
		expect(bootRedirect('login', '/loading')).toBe('login');
		expect(bootRedirect('login', '/')).toBeNull();
	});
});
