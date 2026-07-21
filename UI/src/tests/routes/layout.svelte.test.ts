import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, cleanup, screen, waitFor } from '@testing-library/svelte';
import { createRawSnippet } from 'svelte';
import { SvelteURL } from 'svelte/reactivity';

// The layout's boot guard reads `page.url.pathname` reactively, so the URL mock must be reactive too —
// otherwise the post-boot $effect never re-runs and the latch can't be exercised. A single stable
// `SvelteURL` (created in beforeEach, mutated in place by `setPath`) makes the `.pathname` read inside
// the effect track it. The URL is filled in beforeEach because vi.hoisted runs before module imports
// resolve, so `SvelteURL` isn't available inside the factory.
const {
	goto,
	getTokens,
	onSocketError,
	listenCommand,
	handleSocketReplaced,
	handleAccessRevoked,
	resumeSession,
	playerManager,
	page
} = vi.hoisted(() => ({
	goto: vi.fn(() => Promise.resolve()),
	getTokens: vi.fn(),
	onSocketError: vi.fn(),
	listenCommand: vi.fn(),
	handleSocketReplaced: vi.fn(),
	handleAccessRevoked: vi.fn(),
	resumeSession: vi.fn(),
	playerManager: { name: '' },
	page: { url: new URL('http://localhost/') as URL }
}));

vi.mock('$app/navigation', () => ({ goto }));
vi.mock('$app/paths', () => ({ resolve: (p: string) => p }));
vi.mock('$app/state', () => ({ page }));
vi.mock('$lib/api', () => ({ getTokens, onSocketError, apiSocket: { listenCommand } }));
vi.mock('$lib/engine', () => ({ playerManager, handleSocketReplaced, handleAccessRevoked }));
vi.mock('$lib/engine/session', () => ({ resumeSession }));
vi.mock('$stores', () => ({ toastError: vi.fn() }));
vi.mock('$components', () => ({
	TooltipBase: () => {},
	ToastContainer: () => {},
	ModalHost: () => {}
}));

import Layout from '../../routes/+layout.svelte';
// Deliberately NOT mocked: the boot gate's `bootState.markBooted()` call is the seam #1898's fix on
// `/game` hinges on (see the test below), so this suite asserts against the real module.
import { bootState } from '$lib/engine/boot-state.svelte';

const childSnippet = createRawSnippet(() => ({
	render: () => '<div data-testid="child">child content</div>'
}));

const renderLayout = () => render(Layout, { props: { children: childSnippet } });

// Mutate the stable reactive URL in place (don't reassign) so the effect's `page.url.pathname` read
// re-fires.
const setPath = (pathname: string) => {
	page.url.href = `http://localhost${pathname}`;
};

beforeEach(() => {
	vi.clearAllMocks();
	getTokens.mockReturnValue(null);
	resumeSession.mockResolvedValue('login');
	playerManager.name = '';
	// Fresh reactive URL per test so an in-place mutation re-runs the boot-guard effect.
	page.url = new SvelteURL('http://localhost/');
});

afterEach(cleanup);

describe('root layout socket listeners', () => {
	it('registers a SocketReplaced listener once for the whole app session, independent of the game route (#1836)', () => {
		renderLayout();

		expect(listenCommand).toHaveBeenCalledWith('SocketReplaced', handleSocketReplaced, true);
	});

	it('registers an AccessRevoked listener once for the whole app session, independent of the game route', () => {
		renderLayout();

		expect(listenCommand).toHaveBeenCalledWith('AccessRevoked', handleAccessRevoked, true);
	});
});

describe('root layout boot gate', () => {
	it('renders the route content (no splash) and does not resume when there is no stored session', async () => {
		renderLayout();

		await waitFor(() => expect(screen.getByTestId('child')).toBeTruthy());
		expect(screen.queryByTestId('boot-splash')).toBeNull();
		expect(resumeSession).not.toHaveBeenCalled();
		expect(goto).not.toHaveBeenCalled();
	});

	it('redirects a session-less refresh of a protected route back to login', async () => {
		setPath('/game');
		renderLayout();

		await waitFor(() => expect(goto).toHaveBeenCalledWith('/'));
		expect(resumeSession).not.toHaveBeenCalled();
	});

	it('resumes a fully-restorable session into the game from the login route', async () => {
		getTokens.mockReturnValue({ accessToken: 'a', refreshToken: 'r' });
		resumeSession.mockResolvedValue('game');
		setPath('/');
		renderLayout();

		await waitFor(() => expect(goto).toHaveBeenCalledWith('/game'));
		expect(resumeSession).toHaveBeenCalledTimes(1);
	});

	it('marks the shared boot state resolved on both the no-tokens short-circuit and a token-bearing resume (#1898)', async () => {
		// Both branches hit the same `finally`, which is the seam a route mounted independently of this
		// layout (e.g. `/game`) relies on to know the boot decision has resolved. Spying on the call
		// (rather than only reading the resulting boolean) catches a regression that removes/misplaces
		// the call even though `bootState.booted` — once true — never resets and would otherwise mask it
		// via an earlier test's render.
		const markBooted = vi.spyOn(bootState, 'markBooted');

		renderLayout();
		await waitFor(() => expect(markBooted).toHaveBeenCalledTimes(1));
		expect(bootState.booted).toBe(true);

		markBooted.mockClear();
		getTokens.mockReturnValue({ accessToken: 'a', refreshToken: 'r' });
		resumeSession.mockResolvedValue('game');
		renderLayout();
		await waitFor(() => expect(goto).toHaveBeenCalledWith('/game'));
		expect(markBooted).toHaveBeenCalledTimes(1);

		markBooted.mockRestore();
	});

	it('keeps a fully-restorable session on the route it refreshed on (e.g. /admin)', async () => {
		getTokens.mockReturnValue({ accessToken: 'a', refreshToken: 'r' });
		resumeSession.mockResolvedValue('game');
		// A restored session has a live player, so the post-boot safety net stays inert.
		playerManager.name = 'Hero';
		setPath('/admin');
		renderLayout();

		await waitFor(() => expect(resumeSession).toHaveBeenCalledTimes(1));
		// Splash clears and the route content is revealed in place — no navigation away from /admin.
		await waitFor(() => expect(screen.getByTestId('child')).toBeTruthy());
		expect(goto).not.toHaveBeenCalled();
	});

	it('does not bounce the character-select screen for a missing player, but still guards protected routes', async () => {
		// Boot completes in place (login route, no stored session), arming the post-boot safety net.
		renderLayout();
		await waitFor(() => expect(screen.getByTestId('child')).toBeTruthy());
		expect(goto).not.toHaveBeenCalled();

		// Login navigates to /select before a character is selected/loaded — the safety net must NOT
		// bounce it back to login (the regression that broke the whole login flow).
		playerManager.name = '';
		setPath('/select');
		await waitFor(() => expect(page.url.pathname).toBe('/select'));
		expect(goto).not.toHaveBeenCalled();

		// The effect is still live: losing the player on a genuinely protected route returns to login.
		setPath('/game');
		await waitFor(() => expect(goto).toHaveBeenCalledWith('/'));
	});

	it('falls back to the loading screen when a reference set must be downloaded', async () => {
		getTokens.mockReturnValue({ accessToken: 'a', refreshToken: 'r' });
		resumeSession.mockResolvedValue('loading');
		renderLayout();

		await waitFor(() => expect(goto).toHaveBeenCalledWith('/loading'));
	});

	it('shows the splash while resuming, then reveals the route content', async () => {
		getTokens.mockReturnValue({ accessToken: 'a', refreshToken: 'r' });
		let resolveResume!: (d: string) => void;
		resumeSession.mockReturnValue(new Promise<string>((r) => (resolveResume = r)));
		renderLayout();

		// While the resume is in flight the lightweight splash stands in for the route content.
		await waitFor(() => expect(screen.getByTestId('boot-splash')).toBeTruthy());
		expect(screen.queryByTestId('child')).toBeNull();

		resolveResume('game');
		await waitFor(() => expect(goto).toHaveBeenCalledWith('/game'));
		await waitFor(() => expect(screen.queryByTestId('boot-splash')).toBeNull());
	});
});

describe('root layout boot-guard redirect latch', () => {
	it('issues only one redirect while the post-boot safety net navigation is still settling', async () => {
		// Boot completes in place (a `game` resume on a real in-app route stays put), so the post-boot
		// $effect — not the boot gate's onMount — owns the redirect. With no player name on a protected
		// route it fires goto('/'), which we hold open so `pathname` never settles.
		getTokens.mockReturnValue({ accessToken: 'a', refreshToken: 'r' });
		resumeSession.mockResolvedValue('game');
		playerManager.name = '';
		setPath('/admin');
		let resolveGoto!: () => void;
		goto.mockReturnValue(new Promise<void>((r) => (resolveGoto = r)));
		renderLayout();

		await waitFor(() => expect(goto).toHaveBeenCalledWith('/'));
		expect(goto).toHaveBeenCalledTimes(1);

		// A tracked dep (pathname) changes mid-navigation; without the latch this re-run would fire a
		// redundant second goto('/') before the first navigation resolves.
		setPath('/game');
		await waitFor(() => expect(page.url.pathname).toBe('/game'));
		expect(goto).toHaveBeenCalledTimes(1);

		// Once the navigation settles the latch releases for any genuine future redirect.
		resolveGoto();
	});
});
