import { describe, it, expect, afterEach, beforeEach, vi } from 'vitest';
import { render, fireEvent, cleanup, screen, waitFor } from '@testing-library/svelte';

// Controllable doubles for the submit pipeline: ApiRequest.post is keyed by route so a test can
// stage the CreateAccount / Login responses independently, and the navigation + character-handoff
// side effects are mocked so the happy path can be driven without real I/O.
const { postMock, setTokensMock, gotoMock, handoffSetMock } = vi.hoisted(() => ({
	postMock: vi.fn(),
	setTokensMock: vi.fn(),
	gotoMock: vi.fn(),
	handoffSetMock: vi.fn()
}));

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
	setTokens: setTokensMock
}));
vi.mock('$routes/select/player-select-handoff', () => ({
	playerSelectHandoff: { set: handoffSetMock, take: vi.fn() }
}));

import LoginPage from '../../routes/+page.svelte';

// Login returns the account's characters; the page hands them to the character-select screen and
// navigates there rather than auto-selecting.
const SUMMARIES = [{ id: 1, name: 'Hero', level: 1, currentZoneId: 0, lastActivity: '2026-06-20T00:00:00Z' }];
const LOGIN_OK = {
	status: 200,
	data: { tokens: { accessToken: 'a', refreshToken: 'r' }, playerSummaries: SUMMARIES }
};

const happyRoute = (route: string) =>
	route === 'Login/CreateAccount' ? Promise.resolve({ status: 200 }) : Promise.resolve(LOGIN_OK);

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
	gotoMock.mockClear();
	handoffSetMock.mockClear();
});

afterEach(cleanup);

describe('Login page — submit flow', () => {
	it('signs in, stores the pre-selection tokens, hands off the characters and navigates to select', async () => {
		postMock.mockImplementation(happyRoute);
		render(LoginPage);
		await fillCredentials();

		await submit();

		// The login (pre-selection) token pair is stored so the select screen can rotate it on selection.
		await waitFor(() => expect(setTokensMock).toHaveBeenCalledWith(LOGIN_OK.data.tokens));
		// The account's characters are handed to the select screen, and the page navigates there.
		expect(handoffSetMock).toHaveBeenCalledWith(SUMMARIES);
		await waitFor(() => expect(gotoMock).toHaveBeenCalledWith('/select'));
		// No character is auto-selected here any more.
		expect(postMock).not.toHaveBeenCalledWith('Login/SelectPlayer', expect.anything());
	});

	it('hands off an empty character list and navigates to select (a freshly signed-up account)', async () => {
		// An account with no characters is now a valid state — signup creates the account only, and the
		// first character is created on the select screen (#1256). The page hands off the empty list and
		// proceeds rather than treating it as an error.
		postMock.mockResolvedValue({
			status: 200,
			data: { tokens: { accessToken: 'a', refreshToken: 'r' }, playerSummaries: [] }
		});
		render(LoginPage);
		await fillCredentials();

		await submit();

		await waitFor(() => expect(gotoMock).toHaveBeenCalledWith('/select'));
		expect(handoffSetMock).toHaveBeenCalledWith([]);
	});

	it('surfaces a server error on a rejected login', async () => {
		postMock.mockResolvedValue({ status: 401, error: 'Incorrect username or password.' });
		render(LoginPage);
		await fillCredentials();

		await submit();

		await waitFor(() =>
			expect(screen.getByTestId('status-line').textContent).toContain('Incorrect username or password.')
		);
		expect(gotoMock).not.toHaveBeenCalled();
	});

	it('creates the account then signs in during signup', async () => {
		postMock.mockImplementation(happyRoute);
		render(LoginPage);
		await fireEvent.click(screen.getByTestId('mode-toggle'));
		await fillCredentials('newhero', 'Test1234');
		await fireEvent.input(screen.getByTestId('confirm-input'), { target: { value: 'Test1234' } });

		await submit();

		await waitFor(() => expect(gotoMock).toHaveBeenCalledWith('/select'));
		// Signup creates the account only (no class) — the first character is created on the select screen.
		expect(postMock).toHaveBeenCalledWith('Login/CreateAccount', {
			username: 'newhero',
			password: 'Test1234'
		});
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
		expect(gotoMock).not.toHaveBeenCalled();
	});
});
