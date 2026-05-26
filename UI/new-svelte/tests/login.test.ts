import { expect, test } from '@playwright/test';
import { waitForLoginReady } from './helpers';

test.describe('Login page', () => {
	test('renders login form with expected elements', async ({ page }) => {
		await page.goto('/');

		await expect(page.getByTestId('login-heading')).toHaveText('Welcome back.');
		await expect(page.getByTestId('username-input')).toBeVisible();
		await expect(page.getByTestId('password-input')).toBeVisible();
		await expect(page.getByTestId('submit-button')).toBeVisible();
		await expect(page.getByTestId('mode-toggle')).toBeVisible();
	});

	test('submit button shows Sign In for login mode', async ({ page }) => {
		await page.goto('/');

		const submitButton = page.getByTestId('submit-button');
		await expect(submitButton).toBeVisible();
		await expect(submitButton).toHaveText(/Sign In/i);
	});

	test('mode toggle switches to signup form', async ({ page }) => {
		await page.goto('/');

		await page.getByTestId('mode-toggle').click();

		await expect(page.getByTestId('login-heading')).toHaveText('Begin a new run.');
		await expect(page.getByTestId('confirm-input')).toBeVisible();
		await expect(page.getByTestId('submit-button')).toHaveText(/Create/i);
	});

	test('does not submit with empty credentials', async ({ page }) => {
		await page.goto('/');
		await waitForLoginReady(page);

		const submitButton = page.getByTestId('submit-button');
		await expect(submitButton).toBeDisabled();

		await page.waitForTimeout(500);
		await expect(page).toHaveURL('/');
	});

	test('username and password inputs accept text', async ({ page }) => {
		await page.goto('/');

		const usernameInput = page.getByTestId('username-input');
		const passwordInput = page.getByTestId('password-input');

		await usernameInput.fill('testuser');
		await passwordInput.fill('testpass');

		await expect(usernameInput).toHaveValue('testuser');
		await expect(passwordInput).toHaveValue('testpass');
	});

	test('password input masks characters', async ({ page }) => {
		await page.goto('/');

		const passwordInput = page.getByTestId('password-input');
		await expect(passwordInput).toHaveAttribute('type', 'password');
	});

	test('password toggle reveals and hides password', async ({ page }) => {
		await page.goto('/');

		const passwordInput = page.getByTestId('password-input');
		const toggleBtn = page.getByTestId('password-toggle');

		await expect(passwordInput).toHaveAttribute('type', 'password');

		await toggleBtn.click();
		await expect(passwordInput).toHaveAttribute('type', 'text');

		await toggleBtn.click();
		await expect(passwordInput).toHaveAttribute('type', 'password');
	});

	test('form can be submitted via Enter key', async ({ page }) => {
		await page.goto('/');
		await waitForLoginReady(page);

		await page.getByTestId('username-input').fill('testuser');
		await page.getByTestId('password-input').fill('testpass');

		await page.getByTestId('password-input').press('Enter');
		await page.waitForTimeout(500);

		// Either stays on login (bad creds) or navigates to loading
		const url = page.url();
		expect(url.endsWith('/') || url.includes('/loading')).toBe(true);
	});

	test('signup mode shows strength meter when password is entered', async ({ page }) => {
		await page.goto('/');

		await page.getByTestId('mode-toggle').click();
		await page.getByTestId('password-input').fill('Test123!');

		await expect(page.getByTestId('strength-meter')).toBeVisible();
	});

	test('status line shows validation errors after interaction', async ({ page }) => {
		await page.goto('/');

		const usernameInput = page.getByTestId('username-input');
		await usernameInput.fill('a');
		await usernameInput.blur();

		const statusLine = page.getByTestId('status-line');
		await expect(statusLine).toBeVisible();
	});
});
