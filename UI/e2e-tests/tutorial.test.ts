import { expect, test } from '@playwright/test';
import { createAccountAndLogin } from './helpers';

test.describe('Tutorial tour', () => {
	// The one behavior every other e2e journey works around (via dismissTutorialTourIfPresent in
	// createAccountAndStartGame): a fresh account's first visit to the default Fight screen
	// auto-plays the screen-anchored idle-loop-basics lesson. Pin that it actually plays and can be
	// dismissed, so the workaround stays justified rather than papering over a regression.
	test('idle-loop-basics auto-plays on first fight-screen entry and can be dismissed', async ({ page }) => {
		await createAccountAndLogin(page, 'tt');

		const enterButton = page.getByTestId('enter-button');
		await expect(enterButton).toBeEnabled({ timeout: 10000 });
		await enterButton.click();
		await expect(page).toHaveURL('/game', { timeout: 10000 });

		const tour = page.getByTestId('tutorial-tour');
		await expect(tour).toBeVisible({ timeout: 10000 });
		await expect(tour).toContainText('Welcome to the fight screen');

		await page.getByTestId('tutorial-tour-skip').click();
		await expect(tour).not.toBeVisible();

		// Dismissed, not just hidden: the screen underneath is interactive again.
		await page.getByTestId('sidebar-item-inventory').click();
		await expect(page.getByTestId('inventory-screen')).toBeVisible();
	});
});
