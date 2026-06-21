import { describe, it, expect, afterEach, beforeEach, vi } from 'vitest';
import { render, fireEvent, cleanup, screen, waitFor } from '@testing-library/svelte';

// Controllable doubles for the submit pipeline: ApiRequest.post is keyed by route so a test can
// stage the CreateAccount / Login responses independently, and the session-takeover decision and
// world-entry side effects are mocked so the happy path can be driven without real I/O.
const { postMock, setTokensMock, reportDeviceInfoMock, gotoMock, initializeMock, confirmTakeoverMock } = vi.hoisted(
	() => ({
		postMock: vi.fn(),
		setTokensMock: vi.fn(),
		reportDeviceInfoMock: vi.fn(),
		gotoMock: vi.fn(),
		initializeMock: vi.fn(),
		confirmTakeoverMock: vi.fn()
	})
);

vi.mock('$app/environment', () => ({ browser: true }));
vi.mock('$app/navigation', () => ({ goto: gotoMock }));
vi.mock('$app/paths', () => ({ resolve: (p: string) => p }));
vi.mock('$app/stores', async () => {
	const { readable } = await import('svelte/store');
	return { page: readable({ url: new URL('http://localhost/') }) };
});
vi.mock('$lib/api', () => ({
	ApiRequest: class {
		constructor(private route: string) {}
		post = (body: unknown) => postMock(this.route, body);
		get = vi.fn();
	},
	setTokens: setTokensMock,
	reportDeviceInfo: reportDeviceInfoMock
}));
vi.mock('$lib/engine', () => ({ playerManager: { name: '', initialize: initializeMock } }));
vi.mock('$routes/login/session-takeover', () => ({ confirmSessionTakeover: confirmTakeoverMock }));

import LoginPage from '../../routes/+page.svelte';

// Login returns the account's characters; SelectPlayer (auto-called for the first character) rotates
// the token and returns the loaded player to enter the game with.
const LOGIN_OK = {
	status: 200,
	data: {
		tokens: { accessToken: 'a', refreshToken: 'r' },
		playerSummaries: [{ id: 1, name: 'Hero', level: 1, currentZoneId: 0 }]
	}
};
const SELECT_OK = {
	status: 200,
	data: { tokens: { accessToken: 'a2', refreshToken: 'r2' }, player: { id: 1, name: 'Hero' } }
};

// Routes the staged happy-path responses by endpoint so a test can drive the full
// Login -> SelectPlayer pipeline without real I/O.
const happyRoute = (route: string) => {
	switch (route) {
		case 'Login/CreateAccount':
			return Promise.resolve({ status: 200 });
		case 'Login/SelectPlayer':
			return Promise.resolve(SELECT_OK);
		default:
			return Promise.resolve(LOGIN_OK);
	}
};

const fillCredentials = async (username = 'testuser', password = 'secret1') => {
	await fireEvent.input(screen.getByTestId('username-input'), { target: { value: username } });
	await fireEvent.input(screen.getByTestId('password-input'), { target: { value: password } });
};

const submit = async () => {
	const form = document.querySelector('form');
	if (!form) {
		throw new Error('login form not found');
	}
	await fireEvent.submit(form);
};

beforeEach(() => {
	postMock.mockReset();
	setTokensMock.mockClear();
	reportDeviceInfoMock.mockClear();
	gotoMock.mockClear();
	initializeMock.mockClear();
	confirmTakeoverMock.mockReset();
	confirmTakeoverMock.mockResolvedValue(true);
});

afterEach(cleanup);

describe('Login page — submit flow', () => {
	it('signs in, selects the first character, stores tokens, reports device info and enters the world', async () => {
		postMock.mockImplementation(happyRoute);
		render(LoginPage);
		await fillCredentials();

		await submit();

		// The loaded player from SelectPlayer is what enters the world.
		await waitFor(() => expect(initializeMock).toHaveBeenCalledWith({ id: 1, name: 'Hero' }));
		// Both the pre-selection and rotated token pairs are stored, in order.
		expect(setTokensMock).toHaveBeenCalledWith(LOGIN_OK.data.tokens);
		expect(setTokensMock).toHaveBeenCalledWith(SELECT_OK.data.tokens);
		// SelectPlayer is auto-called for the first character with the login refresh token.
		expect(postMock).toHaveBeenCalledWith('Login/SelectPlayer', { playerId: 1, refreshToken: 'r' });
		expect(reportDeviceInfoMock).toHaveBeenCalledTimes(1);
		// enterWorld navigates to the loading screen after a short delay.
		await waitFor(() => expect(gotoMock).toHaveBeenCalledWith('/loading'));
	});

	it('aborts entry when the player declines the session takeover', async () => {
		postMock.mockImplementation(happyRoute);
		confirmTakeoverMock.mockResolvedValue(false);
		render(LoginPage);
		await fillCredentials();

		await submit();

		// The takeover check runs after selection (a per-player presence check).
		await waitFor(() => expect(confirmTakeoverMock).toHaveBeenCalled());
		expect(setTokensMock).toHaveBeenCalledWith(SELECT_OK.data.tokens);
		// Declining leaves the world un-entered.
		expect(initializeMock).not.toHaveBeenCalled();
		expect(gotoMock).not.toHaveBeenCalled();
	});

	it('surfaces a server error when character selection fails', async () => {
		postMock.mockImplementation((route: string) =>
			route === 'Login/SelectPlayer'
				? Promise.resolve({ status: 404, error: 'Player data not found' })
				: Promise.resolve(LOGIN_OK)
		);
		render(LoginPage);
		await fillCredentials();

		await submit();

		await waitFor(() => expect(screen.getByTestId('status-line').textContent).toContain('Player data not found'));
		expect(initializeMock).not.toHaveBeenCalled();
		expect(gotoMock).not.toHaveBeenCalled();
	});

	it('surfaces a server error on a rejected login', async () => {
		postMock.mockResolvedValue({ status: 401, error: 'Incorrect username or password.' });
		render(LoginPage);
		await fillCredentials();

		await submit();

		await waitFor(() =>
			expect(screen.getByTestId('status-line').textContent).toContain('Incorrect username or password.')
		);
		expect(initializeMock).not.toHaveBeenCalled();
	});

	it('creates the account then signs in during signup', async () => {
		postMock.mockImplementation(happyRoute);
		render(LoginPage);
		await fireEvent.click(screen.getByTestId('mode-toggle'));
		await fillCredentials('newhero', 'Test1234');
		await fireEvent.input(screen.getByTestId('confirm-input'), { target: { value: 'Test1234' } });

		await submit();

		await waitFor(() => expect(initializeMock).toHaveBeenCalledWith({ id: 1, name: 'Hero' }));
		expect(postMock).toHaveBeenCalledWith('Login/CreateAccount', { username: 'newhero', password: 'Test1234' });
		expect(postMock).toHaveBeenCalledWith('Login', { username: 'newhero', password: 'Test1234' });
	});

	it('stops at account creation when CreateAccount fails', async () => {
		postMock.mockResolvedValue({ status: 400, error: 'Username already taken.' });
		render(LoginPage);
		await fireEvent.click(screen.getByTestId('mode-toggle'));
		await fillCredentials('newhero', 'Test1234');
		await fireEvent.input(screen.getByTestId('confirm-input'), { target: { value: 'Test1234' } });

		await submit();

		await waitFor(() => expect(screen.getByTestId('status-line').textContent).toContain('Username already taken.'));
		// The login request is never attempted once creation fails.
		expect(postMock).toHaveBeenCalledTimes(1);
		expect(postMock).toHaveBeenCalledWith('Login/CreateAccount', expect.anything());
		expect(initializeMock).not.toHaveBeenCalled();
	});
});
