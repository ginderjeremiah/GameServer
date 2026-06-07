import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, cleanup, screen, waitFor } from '@testing-library/svelte';
import { createRawSnippet } from 'svelte';

const { goto, getTokens, onSocketError, resumeSession, playerManager, page } = vi.hoisted(() => ({
	goto: vi.fn(() => Promise.resolve()),
	getTokens: vi.fn(),
	onSocketError: vi.fn(),
	resumeSession: vi.fn(),
	playerManager: { name: '' },
	page: { url: new URL('http://localhost/') }
}));

vi.mock('$app/navigation', () => ({ goto }));
vi.mock('$app/paths', () => ({ resolve: (p: string) => p }));
vi.mock('$app/state', () => ({ page }));
vi.mock('$lib/api', () => ({ getTokens, onSocketError }));
vi.mock('$lib/engine', () => ({ playerManager }));
vi.mock('$lib/engine/session', () => ({ resumeSession }));
vi.mock('$stores', () => ({ toastError: vi.fn() }));
vi.mock('$components', () => ({ TooltipBase: () => {}, ToastContainer: () => {} }));

import Layout from '../../routes/+layout.svelte';

const childSnippet = createRawSnippet(() => ({
	render: () => '<div data-testid="child">child content</div>'
}));

const renderLayout = () => render(Layout, { props: { children: childSnippet } });

const setPath = (pathname: string) => {
	page.url = new URL(`http://localhost${pathname}`);
};

beforeEach(() => {
	vi.clearAllMocks();
	getTokens.mockReturnValue(null);
	resumeSession.mockResolvedValue('login');
	playerManager.name = '';
	setPath('/');
});

afterEach(cleanup);

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

	it('resumes straight into the game when the session is fully restorable', async () => {
		getTokens.mockReturnValue({ accessToken: 'a', refreshToken: 'r' });
		resumeSession.mockResolvedValue('game');
		setPath('/game');
		renderLayout();

		await waitFor(() => expect(goto).toHaveBeenCalledWith('/game'));
		expect(resumeSession).toHaveBeenCalledTimes(1);
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
