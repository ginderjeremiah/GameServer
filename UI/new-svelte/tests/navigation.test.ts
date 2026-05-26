import { expect, test } from '@playwright/test';
import { createAccountAndStartGame } from './helpers';

test.describe('Navigation', () => {
	test('switching screens via sidebar', async ({ page }) => {
		await createAccountAndStartGame(page, 'nv');

		await page.getByTestId('sidebar-item-inventory').click();
		await expect(page.getByTestId('screen-container')).toBeVisible();

		await page.getByTestId('sidebar-item-challenges').click();
		await expect(page.getByTestId('screen-container')).toBeVisible();

		await page.getByTestId('sidebar-item-fight').click();
		await expect(page.getByTestId('fight-screen')).toBeVisible();
	});

	test('navigating to admin page', async ({ page }) => {
		await createAccountAndStartGame(page, 'na');

		await page.getByTestId('sidebar-item-admin').click();
		await expect(page).toHaveURL('/admin', { timeout: 5000 });

		await expect(page.locator('.admin-container')).toBeVisible();
		await expect(page.locator('.tool-container')).toBeVisible();
	});

	test('navigating back from admin to game', async ({ page }) => {
		await createAccountAndStartGame(page, 'nb');

		await page.getByTestId('sidebar-item-admin').click();
		await expect(page).toHaveURL('/admin', { timeout: 5000 });

		// Admin page uses its own NavMenu with a "Game" button
		await page.getByText('Game', { exact: true }).click();
		await expect(page).toHaveURL('/game', { timeout: 5000 });

		await expect(page.getByTestId('sidebar-item-fight')).toBeVisible();
	});
});
