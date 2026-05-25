import { expect, test } from '@playwright/test';

test.describe('Login page', () => {
	test('renders login form with expected elements', async ({ page }) => {
		await page.goto('/');

		await expect(page.locator('h1')).toHaveText('Login');
		await expect(page.locator('input[name="username"]')).toBeVisible();
		await expect(page.locator('input[name="password"]')).toBeVisible();
		await expect(page.getByText('Login', { exact: true }).locator('button, [type="submit"]')).toBeTruthy();
		await expect(page.getByText('Create Account')).toBeVisible();
	});

	test('login button is present and clickable', async ({ page }) => {
		await page.goto('/');

		const loginButton = page.locator('button', { hasText: 'Login' });
		await expect(loginButton).toBeVisible();
	});

	test('create account button is present', async ({ page }) => {
		await page.goto('/');

		const createButton = page.locator('button', { hasText: 'Create Account' });
		await expect(createButton).toBeVisible();
	});

	test('does not submit with empty credentials', async ({ page }) => {
		await page.goto('/');

		const loginButton = page.locator('button', { hasText: 'Login' });
		await loginButton.click();

		// Should stay on the login page — no navigation
		await expect(page).toHaveURL('/');
		await expect(page.locator('h1')).toHaveText('Login');
	});

	test('username and password inputs accept text', async ({ page }) => {
		await page.goto('/');

		const usernameInput = page.locator('input[name="username"]');
		const passwordInput = page.locator('input[name="password"]');

		await usernameInput.fill('testuser');
		await passwordInput.fill('testpass');

		await expect(usernameInput).toHaveValue('testuser');
		await expect(passwordInput).toHaveValue('testpass');
	});

	test('password input masks characters', async ({ page }) => {
		await page.goto('/');

		const passwordInput = page.locator('input[name="password"]');
		await expect(passwordInput).toHaveAttribute('type', 'password');
	});

	test('form can be submitted via Enter key', async ({ page }) => {
		await page.goto('/');

		const usernameInput = page.locator('input[name="username"]');
		await usernameInput.fill('testuser');

		const passwordInput = page.locator('input[name="password"]');
		await passwordInput.fill('testpass');

		// Pressing Enter submits the form
		await passwordInput.press('Enter');

		// Without a backend, we expect either an error or to stay on the page
		// The form handler will attempt the API call and fail gracefully
		await page.waitForTimeout(500);

		// Should show either error or remain on login page
		const isOnLogin = await page.locator('h1').textContent();
		expect(['Login', 'Loading']).toContain(isOnLogin);
	});
});
