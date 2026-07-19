import { expect, test } from '@playwright/test';
import { waitForLoginReady } from './helpers';

// Per-field form mechanics (masking, password toggle, strength meter, validation
// messages, text entry) are covered by the jsdom component tests in
// src/tests/routes/login.test.ts. These e2e cases stay focused on what only a real
// browser proves: the page hydrates, the login/signup modes swap, and the form
// guards against empty submission. The full auth round-trip lives in game-flow.test.ts.
test.describe('Login page', () => {
	test('renders the login form', async ({ page }) => {
		await page.goto('/');

		await expect(page.getByTestId('login-heading')).toHaveText('Welcome back.');
		await expect(page.getByTestId('username-input')).toBeVisible();
		await expect(page.getByTestId('password-input')).toBeVisible();
		await expect(page.getByTestId('submit-button')).toHaveText(/Sign In/i);
		await expect(page.getByTestId('mode-toggle')).toBeVisible();
	});

	test('mode toggle switches to the signup form', async ({ page }) => {
		await page.goto('/');
		// Wait for hydration before interacting, otherwise the click can land before the
		// toggle handler is wired and be dropped.
		await waitForLoginReady(page);

		await page.getByTestId('mode-toggle').click();

		await expect(page.getByTestId('login-heading')).toHaveText('Begin a new run.');
		await expect(page.getByTestId('confirm-input')).toBeVisible();
		await expect(page.getByTestId('submit-button')).toHaveText(/Create/i);
	});

	test('does not submit with empty credentials', async ({ page }) => {
		await page.goto('/');
		await waitForLoginReady(page);

		await expect(page.getByTestId('submit-button')).toBeDisabled();

		const authRequests: string[] = [];
		page.on('request', (request) => {
			if (request.url().includes('/api/Auth')) {
				authRequests.push(request.url());
			}
		});

		// A disabled submit button alone doesn't prove the form can't be submitted — attempt the
		// implicit Enter-key submission and confirm it's actually blocked, not just untried.
		await page.getByTestId('password-input').press('Enter');
		await page.waitForLoadState('networkidle');

		expect(authRequests).toHaveLength(0);
		await expect(page).toHaveURL('/');
	});
});
