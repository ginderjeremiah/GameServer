import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, fireEvent, cleanup, screen } from '@testing-library/svelte';
import LoginPage from '../../routes/+page.svelte';

// Mock modules that depend on SvelteKit runtime
vi.mock('$app/environment', () => ({ browser: true }));
vi.mock('$app/navigation', () => ({ goto: vi.fn() }));
vi.mock('$app/stores', async () => {
	const { readable } = await import('svelte/store');
	return { page: readable({ url: new URL('http://localhost/') }) };
});
vi.mock('$lib/api', () => ({
	ApiRequest: class {
		post = vi.fn().mockResolvedValue({ status: 401, error: 'mock' });
		get = vi.fn().mockResolvedValue({ status: 401 });
		constructor() {}
	},
	getTokens: vi.fn(() => null),
	setTokens: vi.fn(),
	reportDeviceInfo: vi.fn(),
	logout: vi.fn()
}));
vi.mock('$stores', () => ({ confirmModal: vi.fn() }));
vi.mock('$lib/engine', () => ({
	playerManager: { name: '', initialize: vi.fn() }
}));

afterEach(cleanup);

describe('Login page', () => {
	it('renders login heading', () => {
		render(LoginPage);
		const heading = screen.getByTestId('login-heading');
		expect(heading.textContent).toBe('Welcome back.');
	});

	it('renders username and password inputs', () => {
		render(LoginPage);
		expect(screen.getByTestId('username-input')).toBeTruthy();
		expect(screen.getByTestId('password-input')).toBeTruthy();
	});

	it('renders submit button', () => {
		render(LoginPage);
		const btn = screen.getByTestId('submit-button');
		expect(btn).toBeTruthy();
		expect(btn.textContent).toMatch(/Sign In/i);
	});

	it('renders mode toggle', () => {
		render(LoginPage);
		const toggle = screen.getByTestId('mode-toggle');
		expect(toggle).toBeTruthy();
		expect(toggle.textContent).toBe('Create one');
	});

	it('switches to signup mode when mode toggle is clicked', async () => {
		render(LoginPage);

		await fireEvent.click(screen.getByTestId('mode-toggle'));

		expect(screen.getByTestId('login-heading').textContent).toBe('Begin a new run.');
		expect(screen.getByTestId('confirm-input')).toBeTruthy();
		expect(screen.getByTestId('submit-button').textContent).toMatch(/Create/i);
	});

	it('password input starts masked', () => {
		render(LoginPage);
		const input = screen.getByTestId('password-input') as HTMLInputElement;
		expect(input.type).toBe('password');
	});

	it('password toggle reveals and hides password', async () => {
		render(LoginPage);
		const input = screen.getByTestId('password-input') as HTMLInputElement;
		const toggle = screen.getByTestId('password-toggle');

		await fireEvent.click(toggle);
		expect(input.type).toBe('text');

		await fireEvent.click(toggle);
		expect(input.type).toBe('password');
	});

	it('submit button is disabled with empty fields', () => {
		render(LoginPage);
		const btn = screen.getByTestId('submit-button') as HTMLButtonElement;
		expect(btn.disabled).toBe(true);
	});

	it('accepts text input in username and password fields', async () => {
		render(LoginPage);
		const username = screen.getByTestId('username-input') as HTMLInputElement;
		const password = screen.getByTestId('password-input') as HTMLInputElement;

		await fireEvent.input(username, { target: { value: 'testuser' } });
		await fireEvent.input(password, { target: { value: 'testpass' } });

		expect(username.value).toBe('testuser');
		expect(password.value).toBe('testpass');
	});

	it('status line is rendered', () => {
		render(LoginPage);
		const status = screen.getByTestId('status-line');
		expect(status).toBeTruthy();
	});

	it('renders strength meter in signup mode after password entry', async () => {
		render(LoginPage);

		await fireEvent.click(screen.getByTestId('mode-toggle'));
		await fireEvent.input(screen.getByTestId('password-input'), { target: { value: 'Test123!' } });

		expect(screen.getByTestId('strength-meter')).toBeTruthy();
	});

	it('associates each input with an accessible label', () => {
		render(LoginPage);

		expect(screen.getByLabelText('Username')).toBe(screen.getByTestId('username-input'));
		expect(screen.getByLabelText('Password')).toBe(screen.getByTestId('password-input'));
	});

	it('sets autocomplete hints for password-manager integration', async () => {
		render(LoginPage);
		const username = screen.getByTestId('username-input') as HTMLInputElement;
		const password = screen.getByTestId('password-input') as HTMLInputElement;

		expect(username.getAttribute('autocomplete')).toBe('username');
		// Sign-in asks for the existing password; signup is choosing a new one.
		expect(password.getAttribute('autocomplete')).toBe('current-password');

		await fireEvent.click(screen.getByTestId('mode-toggle'));
		const signupPassword = screen.getByTestId('password-input') as HTMLInputElement;
		const confirmPassword = screen.getByTestId('confirm-input') as HTMLInputElement;
		expect(signupPassword.getAttribute('autocomplete')).toBe('new-password');
		expect(confirmPassword.getAttribute('autocomplete')).toBe('new-password');
	});

	it('exposes the reveal toggle state via aria-pressed and aria-label', async () => {
		render(LoginPage);
		const toggle = screen.getByTestId('password-toggle');

		expect(toggle.getAttribute('aria-pressed')).toBe('false');
		expect(toggle.getAttribute('aria-label')).toBe('Show password');

		await fireEvent.click(toggle);
		expect(toggle.getAttribute('aria-pressed')).toBe('true');
		expect(toggle.getAttribute('aria-label')).toBe('Hide password');
	});

	it('gives the strength meter a text-equivalent role and value', async () => {
		render(LoginPage);

		await fireEvent.click(screen.getByTestId('mode-toggle'));
		await fireEvent.input(screen.getByTestId('password-input'), { target: { value: 'Test123!' } });

		const meter = screen.getByTestId('strength-meter');
		expect(meter.getAttribute('role')).toBe('meter');
		// 'Test123!' scores length>=8, mixed case, a digit and a symbol => 4 points.
		expect(meter.getAttribute('aria-valuenow')).toBe('4');
		expect(meter.getAttribute('aria-valuetext')).toBe('Strong');
	});
});
