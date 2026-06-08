import { expect, test } from '@playwright/test';
import { createAccountAndStartGame, createAdminAndStartGame, gotoAdmin } from './helpers';

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
		await createAdminAndStartGame(page);
		await gotoAdmin(page);

		// The admin route renders the entity-driven workbench (list + detail panes).
		await expect(page.getByTestId('admin-sidebar')).toBeVisible();
		await expect(page.getByTestId('workbench-list')).toBeVisible({ timeout: 10000 });
	});

	test('navigating back from admin to game', async ({ page }) => {
		await createAdminAndStartGame(page);
		await gotoAdmin(page);

		// The admin sidebar has a dedicated "Return to Game" control.
		await page.getByTestId('admin-return-to-game').click();
		await expect(page).toHaveURL('/game', { timeout: 5000 });

		await expect(page.getByTestId('sidebar-item-fight')).toBeVisible();
	});

	test('hides the admin area from a non-admin player', async ({ page }) => {
		await createAccountAndStartGame(page, 'nq');

		// The other groups render as usual, but the role-gated Admin entry must not for a normal
		// player (the whole Admin nav group collapses when it has no visible screens).
		await expect(page.getByTestId('game-screen')).toBeVisible();
		await expect(page.getByTestId('sidebar-item-fight')).toBeVisible();
		await expect(page.getByTestId('sidebar-item-admin')).toHaveCount(0);
	});
});
