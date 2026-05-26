import { expect, test } from '@playwright/test';
import { createAccountAndLogin, shortUsername, TEST_PASSWORD, waitForLoginReady } from './helpers';

test.describe('Game flow', () => {
	test.describe.configure({ mode: 'serial' });

	let username: string;

	test('can create an account', async ({ page }) => {
		username = await createAccountAndLogin(page, 'gf');
		await expect(page.getByTestId('loading-heading')).toBeVisible();
	});

	test('loading page fetches data and enables enter button', async ({ page }) => {
		await page.goto('/');
		await waitForLoginReady(page);

		await page.getByTestId('username-input').fill(username);
		await page.getByTestId('password-input').fill(TEST_PASSWORD);
		await page.getByTestId('submit-button').click();

		await expect(page).toHaveURL('/loading', { timeout: 10000 });

		const enterButton = page.getByTestId('enter-button');
		await expect(enterButton).toBeEnabled({ timeout: 10000 });

		await enterButton.click();
		await expect(page).toHaveURL('/game', { timeout: 5000 });
	});

	test('game page renders sidebar and battle screen', async ({ page }) => {
		await page.goto('/');
		await waitForLoginReady(page);

		await page.getByTestId('username-input').fill(username);
		await page.getByTestId('password-input').fill(TEST_PASSWORD);
		await page.getByTestId('submit-button').click();

		await expect(page).toHaveURL('/loading', { timeout: 10000 });
		const enterButton = page.getByTestId('enter-button');
		await expect(enterButton).toBeEnabled({ timeout: 10000 });
		await enterButton.click();

		await expect(page).toHaveURL('/game', { timeout: 5000 });

		await expect(page.getByTestId('sidebar')).toBeVisible();
		await expect(page.getByTestId('sidebar-item-fight')).toBeVisible();
		await expect(page.getByTestId('sidebar-item-inventory')).toBeVisible();
		await expect(page.getByTestId('log-panel')).toBeVisible();
	});

	test('session persists on page reload', async ({ page }) => {
		await page.goto('/');
		await waitForLoginReady(page);

		await page.getByTestId('username-input').fill(username);
		await page.getByTestId('password-input').fill(TEST_PASSWORD);
		await page.getByTestId('submit-button').click();

		await expect(page).toHaveURL('/loading', { timeout: 10000 });

		await page.reload();
		await expect(page).toHaveURL('/loading', { timeout: 5000 });
	});
});
