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

const LOGIN_OK = { status: 200, data: { tokens: { accessToken: 'a', refreshToken: 'r' }, player: { name: 'Hero' } } };

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
	it('signs in, stores tokens, reports device info and enters the world', async () => {
		postMock.mockResolvedValue(LOGIN_OK);
		render(LoginPage);
		await fillCredentials();

		await submit();

		await waitFor(() => expect(initializeMock).toHaveBeenCalledWith({ name: 'Hero' }));
		expect(setTokensMock).toHaveBeenCalledWith(LOGIN_OK.data.tokens);
		expect(reportDeviceInfoMock).toHaveBeenCalledTimes(1);
		// enterWorld navigates to the loading screen after a short delay.
		await waitFor(() => expect(gotoMock).toHaveBeenCalledWith('/loading'));
	});

	it('aborts entry when the player declines the session takeover', async () => {
		postMock.mockResolvedValue(LOGIN_OK);
		confirmTakeoverMock.mockResolvedValue(false);
		render(LoginPage);
		await fillCredentials();

		await submit();

		await waitFor(() => expect(confirmTakeoverMock).toHaveBeenCalled());
		expect(setTokensMock).toHaveBeenCalledWith(LOGIN_OK.data.tokens);
		// Declining leaves the world un-entered.
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
		postMock.mockImplementation((route: string) =>
			route === 'Login/CreateAccount' ? Promise.resolve({ status: 200 }) : Promise.resolve(LOGIN_OK)
		);
		render(LoginPage);
		await fireEvent.click(screen.getByTestId('mode-toggle'));
		await fillCredentials('newhero', 'Test1234');
		await fireEvent.input(screen.getByTestId('confirm-input'), { target: { value: 'Test1234' } });

		await submit();

		await waitFor(() => expect(initializeMock).toHaveBeenCalledWith({ name: 'Hero' }));
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
