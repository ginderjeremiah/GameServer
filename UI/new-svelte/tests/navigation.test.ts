import { expect, test } from '@playwright/test';
import { createAccountAndStartGame } from './helpers';

test.describe('Navigation', () => {
	test('switching screens via nav menu', async ({ page }) => {
		await createAccountAndStartGame(page, 'nv');

		await page.getByText('Attributes', { exact: true }).click();
		await expect(page.locator('.screen-container')).toBeVisible();

		await page.getByText('Inventory', { exact: true }).click();
		await expect(page.locator('.screen-container')).toBeVisible();

		await page.getByText('Fight', { exact: true }).click();
		await expect(page.locator('.screen-container')).toBeVisible();
	});

	test('navigating to admin page', async ({ page }) => {
		await createAccountAndStartGame(page, 'na');

		await page.getByText('Admin', { exact: true }).click();
		await expect(page).toHaveURL('/admin', { timeout: 5000 });

		await expect(page.locator('.admin-container')).toBeVisible();
		await expect(page.locator('.tool-container')).toBeVisible();
		await expect(page.locator('button', { hasText: 'Save' })).toBeVisible();
	});

	test('navigating back from admin to game', async ({ page }) => {
		await createAccountAndStartGame(page, 'nb');

		await page.getByText('Admin', { exact: true }).click();
		await expect(page).toHaveURL('/admin', { timeout: 5000 });

		await page.getByText('Game', { exact: true }).click();
		await expect(page).toHaveURL('/game', { timeout: 5000 });

		await expect(page.getByText('Fight')).toBeVisible();
	});
});
