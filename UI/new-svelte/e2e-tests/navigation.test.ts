import { expect, test } from '@playwright/test';
import { createAccountAndStartGame, gotoAdmin } from './helpers';

test.describe('Navigation', () => {
	test('switching screens via sidebar', async ({ page }) => {
		await createAccountAndStartGame(page, 'nv');

		// Fight is the default screen.
		await expect(page.getByTestId('fight-screen')).toBeVisible();

		await page.getByTestId('sidebar-item-inventory').click();
		await expect(page.getByTestId('inventory-screen')).toBeVisible();

		await page.getByTestId('sidebar-item-challenges').click();
		await expect(page.getByTestId('challenges-screen')).toBeVisible();

		await page.getByTestId('sidebar-item-fight').click();
		await expect(page.getByTestId('fight-screen')).toBeVisible();
	});

	test('navigating to admin page', async ({ page }) => {
		await createAccountAndStartGame(page, 'na');
		await gotoAdmin(page);

		// The admin route renders the entity-driven workbench (list + detail panes).
		await expect(page.getByTestId('admin-sidebar')).toBeVisible();
		await expect(page.getByTestId('workbench-list')).toBeVisible({ timeout: 10000 });
	});

	test('navigating back from admin to game', async ({ page }) => {
		await createAccountAndStartGame(page, 'nb');
		await gotoAdmin(page);

		// The admin sidebar has a dedicated "Return to Game" control.
		await page.getByTestId('admin-return-to-game').click();
		await expect(page).toHaveURL('/game', { timeout: 5000 });

		await expect(page.getByTestId('sidebar-item-fight')).toBeVisible();
	});
});
