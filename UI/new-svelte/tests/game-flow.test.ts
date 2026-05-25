import { expect, test } from '@playwright/test';
import { createAccountAndLogin, shortUsername, TEST_PASSWORD, waitForLoginReady } from './helpers';

test.describe('Game flow', () => {
	test.describe.configure({ mode: 'serial' });

	let username: string;

	test('can create an account', async ({ page }) => {
		username = await createAccountAndLogin(page, 'gf');
		await expect(page.locator('h1')).toHaveText('Loading');
	});

	test('loading page fetches data and enables start', async ({ page }) => {
		await page.goto('/');
		await waitForLoginReady(page);

		await page.locator('input[name="username"]').fill(username);
		await page.locator('input[name="password"]').fill(TEST_PASSWORD);
		await page.locator('button', { hasText: 'Login' }).click();

		await expect(page).toHaveURL('/loading', { timeout: 10000 });

		const startButton = page.locator('button', { hasText: 'Start Game' });
		await expect(startButton).toBeEnabled({ timeout: 10000 });

		await startButton.click();
		await expect(page).toHaveURL('/game', { timeout: 5000 });
	});

	test('game page renders nav menu and battle screen', async ({ page }) => {
		await page.goto('/');
		await waitForLoginReady(page);

		await page.locator('input[name="username"]').fill(username);
		await page.locator('input[name="password"]').fill(TEST_PASSWORD);
		await page.locator('button', { hasText: 'Login' }).click();

		await expect(page).toHaveURL('/loading', { timeout: 10000 });
		const startButton = page.locator('button', { hasText: 'Start Game' });
		await expect(startButton).toBeEnabled({ timeout: 10000 });
		await startButton.click();

		await expect(page).toHaveURL('/game', { timeout: 5000 });

		await expect(page.getByText('Fight')).toBeVisible();
		await expect(page.getByText('Attributes')).toBeVisible();
		await expect(page.getByText('Inventory')).toBeVisible();
		await expect(page.locator('.log-container')).toBeVisible();
	});

	test('session persists on page reload', async ({ page }) => {
		await page.goto('/');
		await waitForLoginReady(page);

		await page.locator('input[name="username"]').fill(username);
		await page.locator('input[name="password"]').fill(TEST_PASSWORD);
		await page.locator('button', { hasText: 'Login' }).click();

		await expect(page).toHaveURL('/loading', { timeout: 10000 });

		await page.reload();
		await expect(page).toHaveURL('/loading', { timeout: 5000 });
	});
});
